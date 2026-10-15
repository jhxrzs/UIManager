using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace JH
{
    /// <summary>
    /// 显隐请求和异步恢复共用的主线程队列。空闲时立即消费，执行中提交的工作按顺序等待。
    /// 内部父子和互斥联动直接执行，属于当前工作的同步部分。
    /// </summary>
    internal static class UIOperationDispatcher
    {
        private static readonly Queue<WorkItem> Pending = new();
        private static bool _isExecuting;
        private static RequestContext _currentRequest;

        private readonly struct WorkItem
        {
            internal readonly Action Action;
            internal readonly RequestContext Request;

            internal WorkItem(Action action, RequestContext request)
            {
                Action = action;
                Request = request;
            }
        }

        /// <summary>请求的因果关系不依赖消费器是否空闲，异步恢复仍保留这条链。</summary>
        internal sealed class RequestContext
        {
            internal readonly UIBase Panel;
            // Switch 在入队时尚未确定方向，首次实际执行时再固定为 Show 或 Hide。
            internal bool? IsShow;
            internal readonly RequestContext Parent;
            internal readonly int ExecutionLimit;
            internal readonly string PanelPath;
            internal readonly string Origin;
            internal bool HasStarted;

            internal RequestContext(UIBase panel, bool? isShow, RequestContext parent)
            {
                Panel = panel;
                IsShow = isShow;
                Parent = parent;
                ExecutionLimit = parent?.ExecutionLimit ?? UIOperationSettings.MaxExecutionsPerBehavior;
                PanelPath = GetPanelPath(panel);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Origin = new System.Diagnostics.StackTrace(2, true).ToString();
#else
                Origin = null;
#endif
            }
        }

        /// <summary>提交同步请求。重入请求的调用方已经返回，其异常通过 Unity 日志报告。</summary>
        internal static void Run(Action action)
        {
            Run(action, _currentRequest);
        }

        internal static void Run(UIBase panel, bool? isShow, Action action)
        {
            EnsureMainThread();
            Run(action, new RequestContext(panel, isShow, _currentRequest));
        }

        private static void Run(Action action, RequestContext request)
        {
            EnsureMainThread();
            var deferred = _isExecuting;
            ExceptionDispatchInfo failure = null;
            Enqueue(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    if (deferred) Report(exception);
                    else failure = ExceptionDispatchInfo.Capture(exception);
                }
            }, request);
            failure?.Throw();
        }

        /// <summary>提交异步请求；消费器只启动操作，不等待动画，返回任务跟踪完整结果。</summary>
        internal static UniTask RunAsync(UIBase panel, bool? isShow, Func<UniTask> action,
            CancellationToken cancellationToken)
        {
            if (!PlayerLoopHelper.IsMainThread)
                return UniTask.FromException(new InvalidOperationException("UI显隐请求必须在Unity主线程提交。"));
            if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled(cancellationToken);

            var completion = new UniTaskCompletionSource();
            var request = new RequestContext(panel, isShow, _currentRequest);
            Enqueue(() => ObserveAsync(action, cancellationToken, completion).Forget(), request);
            return completion.Task;
        }

        private static async UniTask ObserveAsync(Func<UniTask> action, CancellationToken cancellationToken,
            UniTaskCompletionSource completion)
        {
            try
            {
                // 排队期间取消的请求不得启动生命周期或取消此前正在执行的操作。
                cancellationToken.ThrowIfCancellationRequested();
                await action();
                completion.TrySetResult();
            }
            catch (OperationCanceledException exception)
            {
                completion.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        private static void EnsureMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread)
                throw new InvalidOperationException("UI显隐请求必须在Unity主线程提交。");
        }

        private static void Report(Exception exception)
        {
            if (exception is UIRequestLoopException loop) Debug.LogException(exception, loop.Panel);
            else Debug.LogException(exception);
        }

        private static void Enqueue(Action action, RequestContext request)
        {
            Pending.Enqueue(new WorkItem(action, request));
            if (_isExecuting) return;

            _isExecuting = true;
            try
            {
                while (Pending.Count > 0)
                {
                    var next = Pending.Dequeue();
                    var previousRequest = _currentRequest;
                    _currentRequest = next.Request;
                    try
                    {
                        next.Action();
                    }
                    catch (Exception exception)
                    {
                        // 一个恢复回调失败不能让其余请求丢失，也不能将消费标记永久锁住。
                        Report(exception);
                    }
                    finally
                    {
                        _currentRequest = previousRequest;
                    }
                }
            }
            finally
            {
                _isExecuting = false;
            }
        }

        private static void Resume(Action continuation, RequestContext request)
        {
            // 自定义行为也可能由工作线程完成；仅在这种情况下需要等待主线程调度。
            if (PlayerLoopHelper.IsMainThread) Enqueue(continuation, request);
            else UniTask.Post(() => Enqueue(continuation, request));
        }

        /// <summary>
        /// 在状态短路、准入判断之后，任何新操作副作用之前检查并计入一次真实执行。
        /// 内部父子与互斥联动属于已有请求，不额外建立公开请求节点。
        /// </summary>
        internal static void BeginBehavior(UIBase panel, bool isShow)
        {
            var request = _currentRequest;
            if (request == null || request.HasStarted ||
                !ReferenceEquals(request.Panel, panel) ||
                (request.IsShow.HasValue && request.IsShow.Value != isShow)) return;

            // 切换请求与普通显隐请求共用同一条来源链，并按实际执行方向计数。
            request.IsShow ??= isShow;

            var count = 0;
            for (var ancestor = request.Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                if (!ancestor.HasStarted || !ReferenceEquals(ancestor.Panel, panel) || ancestor.IsShow != isShow)
                    continue;
                count++;
                if (count < request.ExecutionLimit) continue;

                throw new UIRequestLoopException(panel, isShow, request.PanelPath, request.ExecutionLimit,
                    FormatRequestPath(request), request.Origin);
            }

            request.HasStarted = true;
        }

        private static string GetPanelPath(UIBase panel)
        {
            if (!panel) return "<已销毁UI>";
            var path = panel.name;
            for (var parent = panel.transform.parent; parent; parent = parent.parent)
                path = parent.name + "/" + path;
            return panel.gameObject.scene.name + ":/" + path;
        }

        private static string FormatRequestPath(RequestContext request)
        {
            var chain = new List<RequestContext>();
            for (var node = request; node != null; node = node.Parent) chain.Add(node);
            var path = new StringBuilder();
            for (var index = chain.Count - 1; index >= 0; index--)
            {
                var node = chain[index];
                if (path.Length > 0) path.Append("\n  → ");
                path.Append(node.PanelPath).Append(node.IsShow switch
                {
                    true => ".Show",
                    false => ".Hide",
                    null => ".Switch"
                });
                path.Append(ReferenceEquals(node, request) ? " [本次被阻止]" :
                    node.HasStarted ? " [已执行]" : " [未启动生命周期]");
            }
            return path.ToString();
        }

        /// <summary>
        /// 等待期间释放消费器，完成后将下一段生命周期重新入队。
        /// 已完成的任务在当前调度内继续执行，不额外延后一帧。
        /// </summary>
        internal static DispatchAwaitable Continue(UniTask task) => new(task, _currentRequest);

        internal readonly struct DispatchAwaitable
        {
            private readonly UniTask _task;
            private readonly RequestContext _request;

            internal DispatchAwaitable(UniTask task, RequestContext request)
            {
                _task = task;
                _request = request;
            }
            public Awaiter GetAwaiter() => new(_task.GetAwaiter(), _request);

            internal readonly struct Awaiter : ICriticalNotifyCompletion
            {
                private readonly UniTask.Awaiter _awaiter;
                private readonly RequestContext _request;

                internal Awaiter(UniTask.Awaiter awaiter, RequestContext request)
                {
                    _awaiter = awaiter;
                    _request = request;
                }
                public bool IsCompleted => PlayerLoopHelper.IsMainThread && _isExecuting && _awaiter.IsCompleted;
                public void GetResult() => _awaiter.GetResult();
                public void OnCompleted(Action continuation) => UnsafeOnCompleted(continuation);

                public void UnsafeOnCompleted(Action continuation)
                {
                    var request = _request;
                    if (_awaiter.IsCompleted) Resume(continuation, request);
                    else _awaiter.UnsafeOnCompleted(() => Resume(continuation, request));
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
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
        private static readonly Queue<Action> Pending = new();
        private static bool _isExecuting;

        /// <summary>提交同步请求。重入请求的调用方已经返回，其异常通过 Unity 日志报告。</summary>
        internal static void Run(Action action)
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
                    if (deferred) Debug.LogException(exception);
                    else failure = ExceptionDispatchInfo.Capture(exception);
                }
            });
            failure?.Throw();
        }

        /// <summary>提交异步请求；消费器只启动操作，不等待动画，返回任务跟踪完整结果。</summary>
        internal static UniTask RunAsync(Func<UniTask> action, CancellationToken cancellationToken)
        {
            if (!PlayerLoopHelper.IsMainThread)
                return UniTask.FromException(new InvalidOperationException("UI显隐请求必须在Unity主线程提交。"));
            if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled(cancellationToken);

            var completion = new UniTaskCompletionSource();
            Enqueue(() => ObserveAsync(action, cancellationToken, completion).Forget());
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

        private static void Enqueue(Action action)
        {
            Pending.Enqueue(action);
            if (_isExecuting) return;

            _isExecuting = true;
            try
            {
                while (Pending.Count > 0)
                {
                    var next = Pending.Dequeue();
                    try
                    {
                        next();
                    }
                    catch (Exception exception)
                    {
                        // 一个恢复回调失败不能让其余请求丢失，也不能将消费标记永久锁住。
                        Debug.LogException(exception);
                    }
                }
            }
            finally
            {
                _isExecuting = false;
            }
        }

        private static void Resume(Action continuation)
        {
            // 自定义行为也可能由工作线程完成；仅在这种情况下需要等待主线程调度。
            if (PlayerLoopHelper.IsMainThread) Enqueue(continuation);
            else UniTask.Post(() => Enqueue(continuation));
        }

        /// <summary>
        /// 等待期间释放消费器，完成后将下一段生命周期重新入队。
        /// 已完成的任务在当前调度内继续执行，不额外延后一帧。
        /// </summary>
        internal static DispatchAwaitable Continue(UniTask task) => new(task);

        internal readonly struct DispatchAwaitable
        {
            private readonly UniTask _task;

            internal DispatchAwaitable(UniTask task) => _task = task;
            public Awaiter GetAwaiter() => new(_task.GetAwaiter());

            internal readonly struct Awaiter : ICriticalNotifyCompletion
            {
                private readonly UniTask.Awaiter _awaiter;

                internal Awaiter(UniTask.Awaiter awaiter) => _awaiter = awaiter;
                public bool IsCompleted => PlayerLoopHelper.IsMainThread && _isExecuting && _awaiter.IsCompleted;
                public void GetResult() => _awaiter.GetResult();
                public void OnCompleted(Action continuation) => UnsafeOnCompleted(continuation);

                public void UnsafeOnCompleted(Action continuation)
                {
                    if (_awaiter.IsCompleted) Resume(continuation);
                    else _awaiter.UnsafeOnCompleted(() => Resume(continuation));
                }
            }
        }
    }
}

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
    internal static partial class UIOperationDispatcher
    {
        /// <summary>等待主线程依次消费的显隐工作队列。</summary>
        private static readonly Queue<WorkItem> Pending = new();
        /// <summary>是否正在消费当前同步工作，用于阻止递归消费队列。</summary>
        private static bool _isExecuting;
        /// <summary>当前同步工作所属的请求来源，供重入和异步恢复沿用。</summary>
        private static RequestContext _currentRequest;



        /// <summary>提交同步请求。重入请求的调用方已经返回，其异常通过 Unity 日志报告。</summary>
        /// <param name="action">需要在调度队列内执行的工作。</param>
        internal static void Run(Action action)
        {
            // 内部工作沿用当前请求来源提交到队列。
            Run(action, _currentRequest);
        }

        /// <summary>提交同步工作并保留请求来源，按是否重入决定异常的报告方式。</summary>
        /// <param name="panel">当前操作或诊断针对的界面。</param>
        /// <param name="isShow">显示方向为真，隐藏方向为假；尚未确定的切换请求为空。</param>
        /// <param name="action">需要在调度队列内执行的工作。</param>
        internal static void Run(UIBase panel, bool? isShow, Action action)
        {
            // 公开同步请求必须从 Unity 主线程提交。
            EnsureMainThread();
            // 为公开请求创建新的来源节点并提交工作。
            Run(action, new RequestContext(panel, isShow, _currentRequest));
        }

        /// <summary>提交同步工作并保留请求来源，按是否重入决定异常的报告方式。</summary>
        /// <param name="action">需要在调度队列内执行的工作。</param>
        /// <param name="request">执行与恢复工作时使用的请求来源上下文。</param>
        private static void Run(Action action, RequestContext request)
        {
            // 私有同步调度入口同样强制主线程访问。
            EnsureMainThread();
            // 记录调用方是否因重入而已经无法同步等待异常。
            var deferred = _isExecuting;
            // 为可同步返回给调用方的异常保留原始堆栈。
            ExceptionDispatchInfo failure = null;
            // 将业务工作包装为能够按调用方式报告异常的队列元素。
            Enqueue(() =>
            {
                // 捕获当前同步工作失败，保持队列可继续消费。
                try
                {
                    // 在当前请求上下文内执行提交的工作。
                    action();
                }
                catch (Exception exception)
                {
                    // 已经返回调用方的重入请求通过 Unity 日志报告失败。
                    if (deferred) Report(exception);
                    // 空闲同步请求保存异常，稍后直接返回给原调用方。
                    else failure = ExceptionDispatchInfo.Capture(exception);
                }
            }, request);
            // 队列消费结束后将同步失败按原始堆栈重新抛出。
            failure?.Throw();
        }

        /// <summary>提交异步请求；消费器只启动操作，不等待动画，返回任务跟踪完整结果。</summary>
        /// <param name="panel">当前操作或诊断针对的界面。</param>
        /// <param name="isShow">显示方向为真，隐藏方向为假；尚未确定的切换请求为空。</param>
        /// <param name="action">需要在调度队列内执行的工作。</param>
        /// <param name="cancellationToken">调用方用于取消本次异步操作的令牌。</param>
        /// <returns>跟踪当前请求最终成功、取消或失败的任务。</returns>
        internal static UniTask RunAsync(UIBase panel, bool? isShow, Func<UniTask> action,
            CancellationToken cancellationToken)
        {
            // 非主线程异步调用返回失败任务，不访问调度队列。
            if (!PlayerLoopHelper.IsMainThread)
                // 以任务异常说明显隐请求必须由 Unity 主线程提交。
                return UniTask.FromException(new InvalidOperationException("UI显隐请求必须在Unity主线程提交。"));
            // 已取消的请求不入队，也不创建生命周期副作用。
            if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled(cancellationToken);

            // 创建向调用方报告本次请求结果的完成源。
            var completion = new UniTaskCompletionSource();
            // 保存当前公开异步请求及其直接来源。
            var request = new RequestContext(panel, isShow, _currentRequest);
            // 将异步观察器启动工作入队，不占用队列等待动画。
            Enqueue(() => ObserveAsync(action, cancellationToken, completion).Forget(), request);
            // 返回只跟踪该请求最终结果的公开任务。
            return completion.Task;
        }

        /// <summary>观察一条异步请求，并将完成、取消或异常写入公开任务。</summary>
        /// <param name="action">需要在调度队列内执行的工作。</param>
        /// <param name="cancellationToken">调用方用于取消本次异步操作的令牌。</param>
        /// <param name="completion">向调用方报告该请求最终结果的完成源。</param>
        /// <returns>将操作结果写入公开完成源后结束的观察任务。</returns>
        private static async UniTask ObserveAsync(Func<UniTask> action, CancellationToken cancellationToken,
            UniTaskCompletionSource completion)
        {
            // 将请求执行结果统一转交给公开完成源。
            try
            {
                // 排队期间取消的请求不得启动生命周期或取消此前正在执行的操作。
                cancellationToken.ThrowIfCancellationRequested();
                // 等待实际显隐操作完成，允许调度器处理其他请求。
                await action();
                // 将正常结束报告为该请求成功。
                completion.TrySetResult();
            }
            catch (OperationCanceledException exception)
            {
                // 保留准确取消令牌，向调用方报告操作取消。
                completion.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                // 将业务或调度异常传递给该请求的任务。
                completion.TrySetException(exception);
            }
        }

        /// <summary>拒绝从 Unity 主线程之外提交同步显隐工作。</summary>
        private static void EnsureMainThread()
        {
            // 同步显隐不允许从工作线程访问 Unity 状态。
            if (!PlayerLoopHelper.IsMainThread)
                // 明确抛出线程约束异常，阻止非法调度。
                throw new InvalidOperationException("UI显隐请求必须在Unity主线程提交。");
        }

        /// <summary>将调度异常写入 Unity 日志，循环异常附带对应面板上下文。</summary>
        /// <param name="exception">需要保留原始信息并报告的异常。</param>
        private static void Report(Exception exception)
        {
            // 循环保护异常附带目标面板，方便在 Unity 中定位。
            if (exception is UIRequestLoopException loop) Debug.LogException(exception, loop.Panel);
            // 其他调度异常按普通 Unity 异常日志报告。
            else Debug.LogException(exception);
        }

        /// <summary>将工作及请求来源入队，并在消费器空闲时持续处理待执行项。</summary>
        /// <param name="action">需要在调度队列内执行的工作。</param>
        /// <param name="request">执行与恢复工作时使用的请求来源上下文。</param>
        private static void Enqueue(Action action, RequestContext request)
        {
            // 把工作和请求来源作为一个元素加入先进先出队列。
            Pending.Enqueue(new WorkItem(action, request));
            // 已有消费循环时只入队，不递归启动另一个循环。
            if (_isExecuting) return;

            // 标记当前正在消费，后续重入请求只会追加到队列。
            _isExecuting = true;
            // 即使消费过程出现异常，也必须恢复消费器空闲状态。
            try
            {
                // 持续处理当前队列，包括回调追加的后续请求。
                while (Pending.Count > 0)
                {
                    // 取出最早提交但尚未执行的工作。
                    var next = Pending.Dequeue();
                    // 保存外层请求上下文，供当前工作结束时恢复。
                    var previousRequest = _currentRequest;
                    // 将当前上下文切换到该工作所属请求。
                    _currentRequest = next.Request;
                    // 单项失败不能破坏后续工作或请求上下文恢复。
                    try
                    {
                        // 执行当前队列元素绑定的同步工作。
                        next.Action();
                    }
                    catch (Exception exception)
                    {
                        // 一个恢复回调失败不能让其余请求丢失，也不能将消费标记永久锁住。
                        Report(exception);
                    }
                    finally
                    {
                        // 恢复进入该工作前的请求来源。
                        _currentRequest = previousRequest;
                    }
                }
            }
            finally
            {
                // 队列消费结束后解除执行标记。
                _isExecuting = false;
            }
        }

        /// <summary>将异步延续恢复到主线程调度队列，并保留原请求来源。</summary>
        /// <param name="continuation">等待结束后需要重新调度的生命周期延续。</param>
        /// <param name="request">执行与恢复工作时使用的请求来源上下文。</param>
        private static void Resume(Action continuation, RequestContext request)
        {
            // 自定义行为也可能由工作线程完成；仅在这种情况下需要等待主线程调度。
            if (PlayerLoopHelper.IsMainThread) Enqueue(continuation, request);
            // 工作线程完成的任务先切回主线程，再恢复到队列。
            else UniTask.Post(() => Enqueue(continuation, request));
        }

        /// <summary>
        /// 在状态短路、准入判断之后，任何新操作副作用之前检查并计入一次真实执行。
        /// 内部父子与互斥联动属于已有请求，不额外建立公开请求节点。
        /// </summary>
        /// <param name="panel">当前操作或诊断针对的界面。</param>
        /// <param name="isShow">显示方向为真，隐藏方向为假。</param>
        internal static void BeginBehavior(UIBase panel, bool isShow)
        {
            // 读取当前工作所属请求，判断是否需要登记实际执行。
            var request = _currentRequest;
            // 无公开来源、已登记或目标方向不匹配时不重复计数。
            if (request == null || request.HasStarted ||
                !ReferenceEquals(request.Panel, panel) ||
                // 内部联动或已登记行为沿用既有来源链计数。
                (request.IsShow.HasValue && request.IsShow.Value != isShow)) return;

            // 切换请求与普通显隐请求共用同一条来源链，并按实际执行方向计数。
            request.IsShow ??= isShow;

            // 从零开始统计同一目标同一方向的已执行祖先请求。
            var count = 0;
            // 沿请求来源链向上查找实际执行记录。
            for (var ancestor = request.Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                // 仅统计已开始且目标和方向都相同的祖先请求。
                if (!ancestor.HasStarted || !ReferenceEquals(ancestor.Panel, panel) || ancestor.IsShow != isShow)
                    // 不满足计数条件的祖先不占用行为次数预算。
                    continue;
                // 累计一个已经执行过的同目标同方向请求。
                count++;
                // 尚未达到来源链限制时继续检查更早的请求。
                if (count < request.ExecutionLimit) continue;

                // 超限时携带目标、限制和完整来源链拒绝本次执行。
                throw new UIRequestLoopException(panel, isShow, request.PanelPath, request.ExecutionLimit,
                    FormatRequestPath(request), request.Origin);
            }

            // 通过循环保护后将本请求标记为已开始真实行为。
            request.HasStarted = true;
        }

        /// <summary>保存包含场景名的面板层级路径，供请求诊断使用。</summary>
        /// <param name="panel">当前操作或诊断针对的界面。</param>
        /// <returns>包含场景名的面板路径；面板已销毁时返回占位说明。</returns>
        private static string GetPanelPath(UIBase panel)
        {
            // 目标已销毁时使用明确的诊断占位路径。
            if (!panel) return "<已销毁UI>";
            // 从目标名称开始拼接提交时的面板路径。
            var path = panel.name;
            // 沿 Transform 父链逐层补全路径。
            for (var parent = panel.transform.parent; parent; parent = parent.parent)
                // 将当前祖先名称放到已有路径前面。
                path = parent.name + "/" + path;
            // 加上场景名称，区分不同场景中的相同层级路径。
            return panel.gameObject.scene.name + ":/" + path;
        }

        /// <summary>按从根到当前请求的顺序格式化调度来源及执行状态。</summary>
        /// <param name="request">执行与恢复工作时使用的请求来源上下文。</param>
        /// <returns>标明请求方向及已执行状态的完整来源链字符串。</returns>
        private static string FormatRequestPath(RequestContext request)
        {
            // 保存从当前请求向上收集的来源链。
            var chain = new List<RequestContext>();
            // 沿直接来源逐个记录祖先请求。
            for (var node = request; node != null; node = node.Parent) chain.Add(node);
            // 创建用于构造完整来源链说明的字符串缓冲。
            var path = new StringBuilder();
            // 反向遍历收集结果，从根请求开始输出。
            for (var index = chain.Count - 1; index >= 0; index--)
            {
                // 取得当前需要格式化的请求来源节点。
                var node = chain[index];
                // 在相邻请求之间插入换行和来源箭头。
                if (path.Length > 0) path.Append("\n  → ");
                // 输出面板路径以及当前请求的显隐或切换方向。
                path.Append(node.PanelPath).Append(node.IsShow switch
                {
                    true => ".Show",
                    false => ".Hide",
                    null => ".Switch"
                });
                // 标明当前被阻止请求或祖先是否实际开始生命周期。
                path.Append(ReferenceEquals(node, request) ? " [本次被阻止]" :
                    node.HasStarted ? " [已执行]" : " [未启动生命周期]");
            }
            // 返回完整的请求来源链诊断文本。
            return path.ToString();
        }

        /// <summary>
        /// 等待期间释放消费器，完成后将下一段生命周期重新入队。
        /// 已完成的任务在当前调度内继续执行，不额外延后一帧。
        /// </summary>
        /// <param name="task">需要等待或观察的异步任务。</param>
        /// <returns>携带当前请求来源的可等待对象。</returns>
        internal static DispatchAwaitable Continue(UniTask task) => new(task, _currentRequest);

    }
}

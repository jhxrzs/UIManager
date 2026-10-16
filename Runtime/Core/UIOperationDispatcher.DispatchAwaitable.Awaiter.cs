using System;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;

namespace JH
{
    /// <summary>显隐调度器的分部定义，保存当前文件对应的内部类型。</summary>
    internal static partial class UIOperationDispatcher
    {
        /// <summary>保存待等待任务及其请求来源的调度等待对象。</summary>
        internal readonly partial struct DispatchAwaitable
        {
            /// <summary>包装 UniTask 等待器，使生命周期延续始终回到请求调度队列。</summary>
            internal readonly struct Awaiter : ICriticalNotifyCompletion
            {
                /// <summary>实际观察任务结果与完成通知的 UniTask 等待器。</summary>
                private readonly UniTask.Awaiter _awaiter;
                /// <summary>异步等待开始时保存的请求来源上下文。</summary>
                private readonly RequestContext _request;

                /// <summary>包装 UniTask 等待器，使生命周期延续始终回到请求调度队列。</summary>
                /// <param name="awaiter">底层 UniTask 的等待器。</param>
                /// <param name="request">执行与恢复工作时使用的请求来源上下文。</param>
                internal Awaiter(UniTask.Awaiter awaiter, RequestContext request)
                {
                    // 保存底层任务等待器，以便读取结果和订阅完成。
                    _awaiter = awaiter;
                    // 保存等待开始时的请求来源，避免恢复到其他请求。
                    _request = request;
                }
                /// <summary>仅当任务已完成且当前位于主线程消费阶段时允许同步继续。</summary>
                public bool IsCompleted => PlayerLoopHelper.IsMainThread && _isExecuting && _awaiter.IsCompleted;
                /// <summary>读取底层任务结果，并保留其异常或取消语义。</summary>
                public void GetResult() => _awaiter.GetResult();
                /// <summary>注册等待完成后的生命周期延续。</summary>
                /// <param name="continuation">等待结束后需要重新调度的生命周期延续。</param>
                public void OnCompleted(Action continuation) => UnsafeOnCompleted(continuation);

                /// <summary>在任务完成后将延续重新提交到保存的请求来源下。</summary>
                /// <param name="continuation">等待结束后需要重新调度的生命周期延续。</param>
                public void UnsafeOnCompleted(Action continuation)
                {
                    // 用局部变量保存请求来源供延续闭包使用。
                    var request = _request;
                    // 已完成任务直接申请重新进入原请求调度队列。
                    if (_awaiter.IsCompleted) Resume(continuation, request);
                    // 尚未完成时订阅任务结束通知，再恢复原请求延续。
                    else _awaiter.UnsafeOnCompleted(() => Resume(continuation, request));
                }
            }
        }
    }
}

using Cysharp.Threading.Tasks;

namespace JH
{
    /// <summary>显隐调度器的分部定义，保存当前文件对应的内部类型。</summary>
    internal static partial class UIOperationDispatcher
    {
        /// <summary>保存待观察的任务及其请求来源，供生命周期代码等待并恢复。</summary>
        internal readonly partial struct DispatchAwaitable
        {
            /// <summary>生命周期需要等待完成的异步任务。</summary>
            private readonly UniTask _task;
            /// <summary>异步等待开始时保存的请求来源上下文。</summary>
            private readonly RequestContext _request;

            /// <summary>保存待观察的任务及其请求来源，供生命周期代码等待并恢复。</summary>
            /// <param name="task">需要等待或观察的异步任务。</param>
            /// <param name="request">执行与恢复工作时使用的请求来源上下文。</param>
            internal DispatchAwaitable(UniTask task, RequestContext request)
            {
                // 保存本次生命周期需要等待的任务。
                _task = task;
                // 保存异步恢复必须沿用的请求上下文。
                _request = request;
            }
            /// <summary>创建同时携带底层等待器与请求来源的调度等待器。</summary>
            /// <returns>能够在调度队列内恢复生命周期的等待器。</returns>
            public Awaiter GetAwaiter() => new(_task.GetAwaiter(), _request);

        }
    }
}

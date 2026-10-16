using System;

namespace JH
{
    /// <summary>显隐调度器的分部定义，保存当前文件对应的内部类型。</summary>
    internal static partial class UIOperationDispatcher
    {
        /// <summary>将一段同步工作与其请求来源绑定的队列元素。</summary>
        private readonly struct WorkItem
        {
            /// <summary>该队列元素需要在主线程执行的同步工作。</summary>
            internal readonly Action Action;
            /// <summary>执行该队列元素时应恢复的请求来源。</summary>
            internal readonly RequestContext Request;

            /// <summary>将一段同步工作与其请求来源绑定的队列元素。</summary>
            /// <param name="action">需要在调度队列内执行的工作。</param>
            /// <param name="request">执行与恢复工作时使用的请求来源上下文。</param>
            internal WorkItem(Action action, RequestContext request)
            {
                // 保存该队列元素要执行的同步工作。
                Action = action;
                // 保存执行该工作时要恢复的请求来源。
                Request = request;
            }
        }
    }
}

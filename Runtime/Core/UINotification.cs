using System;
using UnityEngine;

namespace JH
{
    /// <summary>业务通知逐个派发并隔离订阅者异常，不用于显隐 Actions。</summary>
    internal static class UINotification
    {
        /// <summary>按订阅快照逐个派发业务通知，单个订阅者异常不打断其余通知。</summary>
        /// <param name="panel">当前操作或诊断针对的界面。</param>
        /// <param name="eventName">用于异常诊断的业务通知名称。</param>
        /// <param name="handlers">本轮需要逐个派发的订阅委托。</param>
        internal static void Invoke(UIBase panel, string eventName, Action handlers)
        {
            // 没有业务订阅者时无需创建派发快照。
            if (handlers == null) return;
            // 本轮使用稳定快照，回调内增删订阅不改变本轮派发顺序。
            foreach (var subscriber in handlers.GetInvocationList())
            {
                // 隔离单个订阅者的异常，保证后续订阅者仍会执行。
                try
                {
                    // 调用当前无参数业务通知订阅者。
                    ((Action)subscriber)();
                }
                catch (Exception exception)
                {
                    // 将当前订阅者异常与面板及事件信息一起报告。
                    Report(panel, eventName, subscriber, exception);
                }
            }
        }

        /// <summary>按订阅快照逐个派发业务通知，单个订阅者异常不打断其余通知。</summary>
        /// <param name="panel">当前操作或诊断针对的界面。</param>
        /// <param name="eventName">用于异常诊断的业务通知名称。</param>
        /// <param name="handlers">本轮需要逐个派发的订阅委托。</param>
        /// <param name="previousState">状态变化前的生命周期状态。</param>
        /// <param name="currentState">当前生命周期状态。</param>
        internal static void Invoke(UIBase panel, string eventName, Action<UIState, UIState> handlers,
            UIState previousState, UIState currentState)
        {
            // 没有状态订阅者时直接结束派发。
            if (handlers == null) return;
            // 使用本轮稳定订阅快照逐个发送状态变化通知。
            foreach (var subscriber in handlers.GetInvocationList())
            {
                // 隔离单个状态订阅者的失败。
                try
                {
                    // 向当前订阅者传入变化前后的状态。
                    ((Action<UIState, UIState>)subscriber)(previousState, currentState);
                }
                catch (Exception exception)
                {
                    // 报告该状态订阅者异常并继续后续通知。
                    Report(panel, eventName, subscriber, exception);
                }
            }
        }

        /// <summary>为通知订阅者异常附加面板与事件信息后写入 Unity 日志。</summary>
        /// <param name="panel">当前操作或诊断针对的界面。</param>
        /// <param name="eventName">用于异常诊断的业务通知名称。</param>
        /// <param name="subscriber">抛出异常的业务通知订阅者。</param>
        /// <param name="exception">需要保留原始信息并报告的异常。</param>
        private static void Report(UIBase panel, string eventName, Delegate subscriber, Exception exception)
        {
            // 面板销毁后使用明确占位名称构造诊断。
            var panelName = panel ? panel.name : "<已销毁UI>";
            // InnerException 保留原始异常及抛出位置；Unity context 可定位到具体面板。
            Debug.LogException(new UINotificationException(panelName, eventName, subscriber, exception), panel);
        }
    }

}

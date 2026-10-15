using System;
using UnityEngine;

namespace JH
{
    /// <summary>业务通知逐个派发并隔离订阅者异常，不用于显隐 Actions。</summary>
    internal static class UINotification
    {
        internal static void Invoke(UIBase panel, string eventName, Action handlers)
        {
            if (handlers == null) return;
            // 本轮使用稳定快照，回调内增删订阅不改变本轮派发顺序。
            foreach (var subscriber in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)subscriber)();
                }
                catch (Exception exception)
                {
                    Report(panel, eventName, subscriber, exception);
                }
            }
        }

        internal static void Invoke(UIBase panel, string eventName, Action<UIState, UIState> handlers,
            UIState previousState, UIState currentState)
        {
            if (handlers == null) return;
            foreach (var subscriber in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<UIState, UIState>)subscriber)(previousState, currentState);
                }
                catch (Exception exception)
                {
                    Report(panel, eventName, subscriber, exception);
                }
            }
        }

        private static void Report(UIBase panel, string eventName, Delegate subscriber, Exception exception)
        {
            var panelName = panel ? panel.name : "<已销毁UI>";
            // InnerException 保留原始异常及抛出位置；Unity context 可定位到具体面板。
            Debug.LogException(new UINotificationException(panelName, eventName, subscriber, exception), panel);
        }
    }

    internal sealed class UINotificationException : Exception
    {
        internal UINotificationException(string panelName, string eventName, Delegate subscriber, Exception inner)
            : base($"UI通知订阅者异常：面板「{panelName}」，事件 {eventName}，订阅方法 " +
                   $"{subscriber.Method.DeclaringType?.FullName}.{subscriber.Method.Name}。" +
                   "其余订阅者及显隐流程将继续执行。", inner)
        {
        }
    }
}

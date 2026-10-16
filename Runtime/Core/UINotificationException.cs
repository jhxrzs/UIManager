using System;

namespace JH
{
    /// <summary>为业务通知异常附加面板、事件和订阅方法信息，并保留原始异常。</summary>
    internal sealed class UINotificationException : Exception
    {
        /// <summary>为业务通知异常附加面板、事件和订阅方法信息，并保留原始异常。</summary>
        /// <param name="panelName">发生业务通知异常的面板名称。</param>
        /// <param name="eventName">用于异常诊断的业务通知名称。</param>
        /// <param name="subscriber">抛出异常的业务通知订阅者。</param>
        /// <param name="inner">由业务订阅者抛出的原始异常。</param>
        internal UINotificationException(string panelName, string eventName, Delegate subscriber, Exception inner)
            : base($"UI通知订阅者异常：面板「{panelName}」，事件 {eventName}，订阅方法 " +
                   $"{subscriber.Method.DeclaringType?.FullName}.{subscriber.Method.Name}。" +
                   "其余订阅者及显隐流程将继续执行。", inner)
        {
        }
    }
}

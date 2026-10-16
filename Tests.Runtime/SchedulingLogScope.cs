using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的辅助测试设施。</summary>
    public partial class Test_UIBase
    {
        /// <summary>捕获 UIManager 调度日志并负责恢复全局日志开关的测试作用域。</summary>
        private sealed class SchedulingLogScope : IDisposable
        {
            /// <summary>进入日志捕获作用域前的调度日志开关。</summary>
            private readonly bool _previous = UIOperationSettings.EnableSchedulingLogs;
            /// <summary>按回调顺序保存的 UIManager 日志消息。</summary>
            internal readonly List<string> Messages = new();
            /// <summary>与消息逐项对应的 Unity 日志等级。</summary>
            internal readonly List<LogType> Types = new();

            /// <summary>临时配置调度日志开关并捕获框架产生的日志。</summary>
            /// <param name="enabled">作用域内是否启用调度日志。</param>
            internal SchedulingLogScope(bool enabled = true)
            {
                // 应用本作用域指定的调度日志开关。
                UIOperationSettings.EnableSchedulingLogs = enabled;
                // 订阅 Unity 日志回调，收集本作用域中的调度诊断。
                Application.logMessageReceived += Capture;
            }

            /// <summary>仅记录 UIManager 调度日志及其等级，排除其他 Unity 日志。</summary>
            /// <param name="message">用于断言或诊断的消息文本。</param>
            /// <param name="stack">Unity 日志回调提供的异常堆栈。</param>
            /// <param name="type">Unity 日志回调提供的日志等级。</param>
            private void Capture(string message, string stack, LogType type)
            {
                // 忽略没有 UIManager 前缀的其他系统日志。
                if (!message.StartsWith("[UIManager]", StringComparison.Ordinal)) return;
                // 按接收顺序保存调度日志消息。
                Messages.Add(message);
                // 同步保存日志等级，使其与消息索引一一对应。
                Types.Add(type);
            }

            /// <summary>解除日志捕获监听并恢复原来的调度日志开关。</summary>
            public void Dispose()
            {
                // 解除“Application.logMessageReceived”的测试订阅，避免干扰后续用例。
                Application.logMessageReceived -= Capture;
                // 恢复进入作用域前的调度日志开关。
                UIOperationSettings.EnableSchedulingLogs = _previous;
            }
        }
    }
}

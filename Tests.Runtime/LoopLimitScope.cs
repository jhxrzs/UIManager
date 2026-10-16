using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    /// <summary>验证 UIBase 的辅助测试设施。</summary>
    public partial class Test_UIBase
    {
        /// <summary>临时修改请求循环上限并在释放时恢复原值的测试作用域。</summary>
        private sealed class LoopLimitScope : IDisposable
        {
            /// <summary>进入测试作用域前的行为执行上限。</summary>
            private readonly int _previousLimit = UIOperationSettings.MaxExecutionsPerBehavior;

            /// <summary>临时设置请求链执行上限，并保存离开作用域时需要恢复的值。</summary>
            /// <param name="limit">作用域内允许同一行为执行的最大次数。</param>
            internal LoopLimitScope(int limit) => UIOperationSettings.MaxExecutionsPerBehavior = limit;
            /// <summary>恢复进入作用域前的请求链执行上限。</summary>
            public void Dispose() => UIOperationSettings.MaxExecutionsPerBehavior = _previousLimit;
        }
    }
}

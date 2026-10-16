using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    /// <summary>在异步显示入口同步抛错的测试界面，用于验证已启动任务仍被完整观察。</summary>
    public sealed class ThrowingShowUI : UIBase
    {
        /// <summary>在返回任务前主动抛出异常，验证调用方仍观察已启动的祖先任务。</summary>
        /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
        /// <returns>此探针始终同步抛出异常，不返回可等待任务。</returns>
        protected override UniTask OnUIShowAsync(CancellationToken cancellationToken)
        {
            // 抛出预设异常，验证调用方的失败传播与收尾行为。
            throw new InvalidOperationException("override failed");
        }
    }
}

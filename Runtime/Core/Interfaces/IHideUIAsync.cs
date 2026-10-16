using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace JH
{
    /// <summary>
    ///  UI异步隐藏接口。
    /// </summary>
    public interface IHideUIAsync : IHideUIBase
    {
        /// <summary>
        /// 注册可取消的异步隐藏表现，例如淡出动画或音效。
        /// </summary>
        event Func<CancellationToken, UniTask> HideAsyncActions;

        /// <summary>
        ///  提交异步隐藏请求，返回任务跟踪该请求的完整结果，等待期间允许后续请求打断。
        /// </summary>
        /// <param name="cancellationToken">用于取消本次异步隐藏请求的令牌。</param>
        /// <returns>跟踪本次隐藏请求完整结果的任务。</returns>
        UniTask HideAsync(CancellationToken cancellationToken);
    }
}

using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace JH
{
    /// <summary>提交可取消的异步显示请求并注册异步显示表现的接口。</summary>
    public interface IShowUIAsync : IShowUIBase
    {
        /// <summary>
        /// 注册可取消的异步显示表现，例如淡入动画或音效。
        /// </summary>
        event Func<CancellationToken, UniTask> ShowAsyncActions;

        /// <summary>
        ///  提交异步显示请求，返回任务跟踪该请求的完整结果，等待期间允许后续请求打断。
        /// </summary>
        /// <param name="cancellationToken">用于取消本次异步显示请求的令牌。</param>
        /// <returns>跟踪本次显示请求完整结果的任务。</returns>
        UniTask ShowAsync(CancellationToken cancellationToken);
    }
}

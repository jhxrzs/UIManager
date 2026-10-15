using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace JH
{
    public interface IShowUIBase : IUIBase
    {
        /// <summary>
        ///  UI显示前同步通知；单个订阅者异常不会中断其余通知或显隐流程。
        /// </summary>
        event Action BeforeUIShowEvent;

        /// <summary>
        ///  UI显示后同步通知；单个订阅者异常不会中断其余通知或显隐流程。
        /// </summary>
        event Action AfterUIShowedEvent;
    }

    public interface IShowUI : IShowUIBase
    {
        /// <summary>
        ///  暴露给外部，增加UI同步显示行为的事件，注册此事件以增加显示行为，比如gameObject.SetActive(true)...等
        /// </summary>
        event Action ShowActions;

        /// <summary>
        ///  提交同步显示请求。空闲时立即执行；调度回调中的请求在当前工作返回后执行。
        /// </summary>
        void Show();
    }

    public interface IShowUIAsync : IShowUIBase
    {
        /// <summary>
        ///  暴露给外部，增加UI异步显示行为的事件
        /// </summary>
        event Func<CancellationToken, UniTask> ShowAsyncActions;

        /// <summary>
        ///  提交异步显示请求，返回任务跟踪该请求的完整结果，等待期间允许后续请求打断。
        /// </summary>
        /// <param name="cancellationToken"> 取消异步的token</param>
        /// <returns>返回异步任务</returns>
        UniTask ShowAsync(CancellationToken cancellationToken);
    }
}

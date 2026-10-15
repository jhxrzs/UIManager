using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace JH
{
    /// <summary>
    ///  UI隐藏生命周期的基础接口。
    /// </summary>
    public interface IHideUIBase : IUIBase
    {
        /// <summary>
        ///  UI隐藏前同步通知；单个订阅者异常不会中断其余通知或显隐流程。
        /// </summary>
        event Action BeforeUIHideEvent;

        /// <summary>
        ///  UI隐藏后同步通知；单个订阅者异常不会中断其余通知或显隐流程。
        /// </summary>
        event Action AfterUIHidedEvent;
    }

    /// <summary>
    ///  UI同步隐藏接口。
    /// </summary>
    public interface IHideUI : IHideUIBase
    {
        /// <summary>
        ///  暴露给外部，增加UI哦同步隐藏行为的事件，注册此事件以增加隐藏行为，比如gameObject.SetActive(false)...等
        /// </summary>
        event Action HideActions;

        /// <summary>
        ///  提交同步隐藏请求。空闲时立即执行；调度回调中的请求在当前工作返回后执行。
        /// </summary>
        void Hide();
    }

    /// <summary>
    ///  UI异步隐藏接口。
    /// </summary>
    public interface IHideUIAsync : IHideUIBase
    {
        /// <summary>
        ///  暴露给外部，增加UI异步隐藏行为的事件,可以注册此事件以增加异步显示行为，比如fadeIn/fadeOut/音效/...等
        /// </summary>
        event Func<CancellationToken, UniTask> HideAsyncActions;

        /// <summary>
        ///  提交异步隐藏请求，返回任务跟踪该请求的完整结果，等待期间允许后续请求打断。
        /// </summary>
        /// <param name="cancellationToken"> 取消异步的token</param>
        /// <returns> 返回异步任务</returns>
        UniTask HideAsync(CancellationToken cancellationToken);
    }
}

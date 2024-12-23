using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace UIManager
{
    public interface IHideUIBase:IUIBase
    {
        /// <summary>
        ///  UI隐藏前触发
        /// </summary>
         event Action BeforeUIHideEvent;

        /// <summary>
        ///  UI隐藏时触发
        /// </summary>
         event Action AfterUIHidedEvent;
    }

    public interface IHideUI : IHideUIBase
    {
        /// <summary>
        ///  暴露给外部，增加UI哦同步隐藏行为的事件，注册此事件以增加隐藏行为，比如gameObject.SetActive(false)...等
        /// </summary>
        event Action HideActions;
        
        /// <summary>
        ///  隐藏UI
        /// </summary>
        void Hide();
    }

    public interface IHideUIAsync : IShowUIBase
    {
        /// <summary>
        ///  暴露给外部，增加UI异步隐藏行为的事件,可以注册此事件以增加异步显示行为，比如fadeIn/fadeOut/音效/...等
        /// </summary>
         event Func<CancellationToken, UniTask> HideAsyncActions;

        /// <summary>
        ///  异步隐藏UI
        /// </summary>
        /// <param name="cancellationToken"> 取消异步的token</param>
        /// <returns> 返回异步任务</returns>
        UniTask HideAsync(CancellationToken cancellationToken );
    }
}
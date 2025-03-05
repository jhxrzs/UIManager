using System;
using System.Threading;

namespace JH
{
    public interface IShowUIBase : IUIBase
    {
        /// <summary>
        ///  UI显示前触发
        /// </summary>
        event Action BeforeUIShowEvent;

        /// <summary>
        ///  UI显示后触发
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
        ///  显示UI
        /// </summary>
        void Show();
    }

    public interface IShowUIAsync : IShowUIBase
    {
        /// <summary>
        ///  暴露给外部，增加UI异步显示行为的事件
        /// </summary>
        event Func<CancellationToken, JTask> ShowAsyncActions;

        /// <summary>
        ///  当UI异步显示时执行的逻辑
        /// </summary>
        /// <param name="cancellationToken"> 取消异步的token</param>
        /// <returns>返回异步任务</returns>
        JTask ShowAsync(CancellationToken cancellationToken);
    }
}
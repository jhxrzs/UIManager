using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace JH
{
    /// <summary>提供界面状态与显示前后通知的基础接口。</summary>
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


}

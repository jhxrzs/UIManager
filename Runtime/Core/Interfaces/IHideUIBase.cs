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


}

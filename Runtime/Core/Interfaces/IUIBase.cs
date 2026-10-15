using System;

namespace JH
{
    /// <summary>
    /// UI面板的基接口
    /// </summary>
    public interface IUIBase
    {
        /// <summary>
        ///  UI状态
        /// </summary>
        UIState State { get; }

        /// <summary>
        ///  当UI状态改变时触发
        ///  参数： 原始状态，新状态
        ///  同步订阅者逐个执行；异常单独记录，不中断其余订阅者或显隐流程。
        /// </summary>
        public event Action<UIState, UIState> OnStateChanged;
    }
}

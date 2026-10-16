using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace JH
{
    /// <summary>
    ///  UI同步隐藏接口。
    /// </summary>
    public interface IHideUI : IHideUIBase
    {
        /// <summary>
        /// 注册同步隐藏表现，例如关闭界面对象。
        /// </summary>
        event Action HideActions;

        /// <summary>
        ///  提交同步隐藏请求。空闲时立即执行；调度回调中的请求在当前工作返回后执行。
        /// </summary>
        void Hide();
    }
}

using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace JH
{
    /// <summary>提交同步显示请求并注册同步显示表现的接口。</summary>
    public interface IShowUI : IShowUIBase
    {
        /// <summary>
        /// 注册同步显示表现，例如激活界面对象。
        /// </summary>
        event Action ShowActions;

        /// <summary>
        ///  提交同步显示请求。空闲时立即执行；调度回调中的请求在当前工作返回后执行。
        /// </summary>
        void Show();
    }
}

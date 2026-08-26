using System;
using UnityEngine;

namespace JH
{
    /// <summary>
    ///  显示隐藏的交互实体类。
    /// </summary>
    [Serializable]
    public abstract class ShowHideInteractionEntity
    {
        /// <summary>
        ///  显隐行为。
        /// </summary>
        public abstract ShowHideAction Action { get; }

        /// <summary>
        ///  是否异步执行显隐行为。
        /// </summary>
        [field: SerializeField]
        public bool IsAsync { get; set; }

        /// <summary>
        ///  初始化UI的显隐交互。
        /// </summary>
        /// <param name="ui">需要执行显隐行为的UI。</param>
        public abstract void Init(UIBase ui);

        /// <summary>
        ///  按照当前配置执行显隐行为。
        /// </summary>
        /// <param name="ui">需要执行显隐行为的UI。</param>
        protected void Execute(UIBase ui)
        {
            // 使用当前实体配置的显隐行为进入统一执行入口。
            Execute(ui, Action);
        }

        /// <summary>
        ///  按照指定行为执行UI显隐。
        /// </summary>
        /// <param name="ui">需要执行显隐行为的UI。</param>
        /// <param name="action">本次需要执行的显隐行为。</param>
        protected void Execute(UIBase ui, ShowHideAction action)
        {
            // 显示行为统一进入显示方法，避免各类输入重复维护同步与异步分支。
            if (action == ShowHideAction.ToShow)
            {
                ExecuteShow(ui);
                return;
            }

            // 隐藏行为统一进入隐藏方法。
            if (action == ShowHideAction.ToHide)
            {
                ExecuteHide(ui);
                return;
            }

            // 非切换行为无需继续处理。
            if (action != ShowHideAction.Switch) return;

            // 已显示或正在显示时，切换行为应转为隐藏。
            if (ui.State is UIState.Showed or UIState.Showing)
            {
                ExecuteHide(ui);
                return;
            }

            // 其余状态下，切换行为应转为显示。
            ExecuteShow(ui);
        }

        /// <summary>
        ///  执行UI显示行为。
        /// </summary>
        /// <param name="ui">需要显示的UI。</param>
        private void ExecuteShow(UIBase ui)
        {
            // 异步配置使用UI销毁令牌，保证对象销毁时可以停止显隐流程。
            if (IsAsync)
            {
                _ = ui.ShowAsync(ui.destroyCancellationToken);
                return;
            }

            // 同步配置直接执行显示。
            ui.Show();
        }

        /// <summary>
        ///  执行UI隐藏行为。
        /// </summary>
        /// <param name="ui">需要隐藏的UI。</param>
        private void ExecuteHide(UIBase ui)
        {
            // 异步配置使用UI销毁令牌，保证对象销毁时可以停止显隐流程。
            if (IsAsync)
            {
                _ = ui.HideAsync(ui.destroyCancellationToken);
                return;
            }

            // 同步配置直接执行隐藏。
            ui.Hide();
        }
    }
}

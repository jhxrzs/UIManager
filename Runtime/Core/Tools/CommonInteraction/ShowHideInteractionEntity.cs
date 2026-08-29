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
            // 异步配置进入受控事件边界，保证JTask结果和异常都会被消费。
            if (IsAsync)
            {
                ExecuteAsync(ui, ShowHideAction.ToShow);
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
            // 异步配置进入受控事件边界，保证JTask结果和异常都会被消费。
            if (IsAsync)
            {
                ExecuteAsync(ui, ShowHideAction.ToHide);
                return;
            }

            // 同步配置直接执行隐藏。
            ui.Hide();
        }

        /// <summary>
        ///  在输入事件边界等待并消费一次异步显隐行为。
        /// </summary>
        /// <param name="ui">需要执行显隐行为的UI。</param>
        /// <param name="action">本次已经确定的显示或隐藏行为。</param>
        /// <remarks>
        ///  此方法是受控的async void事件边界，内部会处理显隐替换取消和业务异常。
        /// </remarks>
        private async void ExecuteAsync(UIBase ui, ShowHideAction action)
        {
            try
            {
                // 在首次等待前保存销毁令牌，避免UI销毁后再次访问Unity对象属性。
                var cancellationToken = ui.destroyCancellationToken;

                // 显示行为等待UIBase完整执行父链、互斥、动画和生命周期流程。
                if (action == ShowHideAction.ToShow)
                {
                    await ui.ShowAsync(cancellationToken);
                    return;
                }

                // 当前入口只会接收已经确定的显示或隐藏行为，剩余分支执行异步隐藏。
                await ui.HideAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 后续显隐请求替换当前流程或UI销毁时，取消属于预期生命周期结果。
            }
            catch (Exception exception)
            {
                // async void边界必须消费并上报业务异常，避免形成全局未处理异常。
                Debug.LogException(exception, ui);
            }
        }
    }
}

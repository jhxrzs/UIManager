using Cysharp.Threading.Tasks;
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

        /// <summary>解除当前交互的绑定；重复调用安全，不影响控件上的其他监听。</summary>
        public virtual void Unbind()
        {
        }

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
            // 同一轮控件通知的前序监听可能已经销毁面板，旧通知快照仍可能调用到这里。
            if (!ui) return;

            // 主动禁用交互组件时暂停输入；仅 GameObject 失活仍保留外部唤醒能力。
            if (ui is UIBaseWithShowHideInteraction interaction && !interaction.enabled) return;

            // 显示行为统一进入显示方法，避免各类输入重复维护同步与异步分支。
            if (action == ShowHideAction.ToShow)
            {
                // 按实体的同步或异步配置提交显示。
                ExecuteShow(ui);
                // 显示请求已经处理，不再进入其他分支。
                return;
            }

            // 隐藏行为统一进入隐藏方法。
            if (action == ShowHideAction.ToHide)
            {
                // 按实体的同步或异步配置提交隐藏。
                ExecuteHide(ui);
                // 隐藏请求已经处理，不再进入切换分支。
                return;
            }

            // 非切换行为无需继续处理。
            if (action != ShowHideAction.Switch) return;

            // 保留切换意图，实际轮到此请求时才按最新状态选择显隐方向。
            if (IsAsync)
            {
                // 提交保留切换意图的异步请求。
                ExecuteAsync(ui, ShowHideAction.Switch);
                // 异步入口负责后续处理，本次事件调用结束。
                return;
            }

            // 同步配置直接提交切换。
            ui.Switch();
        }

        /// <summary>
        ///  执行UI显示行为。
        /// </summary>
        /// <param name="ui">需要显示的UI。</param>
        private void ExecuteShow(UIBase ui)
        {
            // 异步配置进入受控事件边界，保证UniTask结果和异常都会被消费。
            if (IsAsync)
            {
                // 将显示交给负责消费结果的异步入口。
                ExecuteAsync(ui, ShowHideAction.ToShow);
                // 避免异步显示后再次执行同步显示。
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
            // 异步配置进入受控事件边界，保证UniTask结果和异常都会被消费。
            if (IsAsync)
            {
                // 将隐藏交给负责消费结果的异步入口。
                ExecuteAsync(ui, ShowHideAction.ToHide);
                // 避免异步隐藏后再次执行同步隐藏。
                return;
            }

            // 同步配置直接执行隐藏。
            ui.Hide();
        }

        /// <summary>
        ///  在输入事件边界等待并消费一次异步显隐行为。
        /// </summary>
        /// <param name="ui">需要执行显隐行为的UI。</param>
        /// <param name="action">本次提交的显示、隐藏或切换行为。</param>
        /// <remarks>
        ///  此方法是受控的async void事件边界，内部会处理显隐替换取消和业务异常。
        /// </remarks>
        private async void ExecuteAsync(UIBase ui, ShowHideAction action)
        {
            // 在事件边界捕获取消及业务异常，防止异步异常逃逸。
            try
            {
                // 在首次等待前保存销毁令牌，避免UI销毁后再次访问Unity对象属性。
                var cancellationToken = ui.destroyCancellationToken;

                // 显示行为等待UIBase完整执行父链、互斥、动画和生命周期流程。
                if (action == ShowHideAction.ToShow)
                {
                    // 等待显示完成或收到取消通知。
                    await ui.ShowAsync(cancellationToken);
                    // 显示流程结束后退出事件边界。
                    return;
                }

                // 隐藏请求使用隐藏流程及同一销毁令牌。
                if (action == ShowHideAction.ToHide)
                {
                    // 等待隐藏完成或收到取消通知。
                    await ui.HideAsync(cancellationToken);
                    // 隐藏流程结束后退出事件边界。
                    return;
                }

                // 切换方向由队列执行时决定，任务仍跟踪完整生命周期。
                await ui.SwitchAsync(cancellationToken);
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

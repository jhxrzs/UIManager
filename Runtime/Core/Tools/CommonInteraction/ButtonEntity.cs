using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JH
{
    /// <summary>
    ///   按钮的显隐交互实体类。
    /// </summary>
    [Serializable]
    public class ButtonEntity : ShowHideInteractionEntity
    {
        /// <summary>
        ///  按钮触发后的显隐行为。
        /// </summary>
        [SerializeField, Header("显隐行为")]
        private ShowHideAction action;

        /// <summary>
        ///  用于触发显隐行为的按钮。
        /// </summary>
        [SerializeField, Header("显隐按钮")] private Button button;

        /// <summary>
        ///  实际注册监听的按钮事件，用于在配置引用改变后准确解绑。
        /// </summary>
        private Button.ButtonClickedEvent _boundClickEvent;
        /// <summary>
        ///  本实体注册的点击回调，解绑时只移除此监听。
        /// </summary>
        private UnityAction _clickListener;

        /// <inheritdoc />
        public override ShowHideAction Action => action;

        /// <inheritdoc />
        public override void Init(UIBase ui)
        {
            // 先解除上次初始化的监听，避免重复注册。
            Unbind();
            // 按钮或目标面板已经失效时不再绑定。
            if (!button || !ui) return;

            // 按钮点击后进入基类维护的统一显隐执行入口。
            _clickListener = () =>
            {
                // 具体的同步、异步和切换逻辑只由基类维护。
                Execute(ui);
            };
            // 保存实际绑定的事件，后续配置引用改变或控件销毁时仍能移除旧监听。
            _boundClickEvent = button.onClick;
            // 把本实体的监听加入保存的按钮事件。
            _boundClickEvent.AddListener(_clickListener);
        }

        /// <inheritdoc />
        public override void Unbind()
        {
            // 只移除本实体注册的监听，保留控件上的其他回调。
            if (_clickListener != null) _boundClickEvent?.RemoveListener(_clickListener);
            // 释放旧按钮事件的引用。
            _boundClickEvent = null;
            // 清除监听缓存，使重复解绑保持安全。
            _clickListener = null;
        }
    }
}

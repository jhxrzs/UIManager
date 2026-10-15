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

        private Button.ButtonClickedEvent _boundClickEvent;
        private UnityAction _clickListener;

        /// <inheritdoc />
        public override ShowHideAction Action => action;

        /// <inheritdoc />
        public override void Init(UIBase ui)
        {
            Unbind();
            if (!button || !ui) return;

            // 按钮点击后进入基类维护的统一显隐执行入口。
            _clickListener = () =>
            {
                // 具体的同步、异步和切换逻辑只由基类维护。
                Execute(ui);
            };
            // 保存实际绑定的事件，后续配置引用改变或控件销毁时仍能移除旧监听。
            _boundClickEvent = button.onClick;
            _boundClickEvent.AddListener(_clickListener);
        }

        /// <inheritdoc />
        public override void Unbind()
        {
            if (_clickListener != null) _boundClickEvent?.RemoveListener(_clickListener);
            _boundClickEvent = null;
            _clickListener = null;
        }
    }
}

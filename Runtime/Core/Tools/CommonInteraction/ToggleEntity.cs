using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JH
{
    /// <summary>
    ///  Toggle的显隐交互实体类。
    /// </summary>
    [Serializable]
    public class ToggleEntity : ShowHideInteractionEntity
    {
        /// <summary>
        ///  用于切换显隐状态的Toggle。
        /// </summary>
        [SerializeField, Header("显隐toggle")] private Toggle toggle;

        /// <summary>
        ///  实际注册监听的 Toggle 事件，用于在配置引用改变后准确解绑。
        /// </summary>
        private Toggle.ToggleEvent _boundValueChangedEvent;
        /// <summary>
        ///  本实体注册的值变化回调，解绑时只移除此监听。
        /// </summary>
        private UnityAction<bool> _valueChangedListener;

        /// <inheritdoc />
        public override ShowHideAction Action => ShowHideAction.Switch;

        /// <inheritdoc />
        public override void Init(UIBase ui)
        {
            // 先清理上次初始化的监听，避免重复切换。
            Unbind();
            // 控件或目标面板已经失效时不再绑定。
            if (!toggle || !ui) return;

            // Toggle值改变后根据当前值确定本次需要执行的具体行为。
            _valueChangedListener = isOn =>
            {
                // Toggle开启时显示，关闭时隐藏，并复用基类中的统一执行逻辑。
                Execute(ui, isOn ? ShowHideAction.ToShow : ShowHideAction.ToHide);
            };
            // 保存实际订阅的事件，以便配置改变后仍可解绑。
            _boundValueChangedEvent = toggle.onValueChanged;
            // 把本实体的状态监听加入控件事件。
            _boundValueChangedEvent.AddListener(_valueChangedListener);
        }

        /// <inheritdoc />
        public override void Unbind()
        {
            // 只解除本实体注册的监听，保留其他订阅者。
            if (_valueChangedListener != null) _boundValueChangedEvent?.RemoveListener(_valueChangedListener);
            // 释放旧控件事件的引用。
            _boundValueChangedEvent = null;
            // 清除监听缓存，使后续解绑可以重复调用。
            _valueChangedListener = null;
        }
    }
}

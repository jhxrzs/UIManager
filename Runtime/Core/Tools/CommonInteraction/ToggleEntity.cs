using System;
using UnityEngine;
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

        /// <inheritdoc />
        public override ShowHideAction Action => ShowHideAction.Switch;

        /// <inheritdoc />
        public override void Init(UIBase ui)
        {
            // Toggle值改变后根据当前值确定本次需要执行的具体行为。
            toggle.onValueChanged.AddListener(isOn =>
            {
                // Toggle开启时显示，关闭时隐藏，并复用基类中的统一执行逻辑。
                Execute(ui, isOn ? ShowHideAction.ToShow : ShowHideAction.ToHide);
            });
        }
    }
}

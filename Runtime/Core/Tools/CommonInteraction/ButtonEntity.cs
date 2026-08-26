using System;
using UnityEngine;
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

        /// <inheritdoc />
        public override ShowHideAction Action => action;

        /// <inheritdoc />
        public override void Init(UIBase ui)
        {
            // 按钮点击后进入基类维护的统一显隐执行入口。
            button.onClick.AddListener(() =>
            {
                // 具体的同步、异步和切换逻辑只由基类维护。
                Execute(ui);
            });
        }
    }
}

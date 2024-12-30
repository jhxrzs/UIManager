using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace UIManager
{
    /// <summary>
    ///  toggle的显隐交互实体类
    /// </summary>
    [Serializable]
    public class ToggleEntity : ShowHideInteractionEntity
    {
        [SerializeField, Header("显隐toggle")] private Toggle toggle;

        /// <summary>
        ///  显隐行为
        /// </summary>
        public override ShowHideAction Action => ShowHideAction.Switch;

        public override void Init(UIBase ui)
        {
            toggle.onValueChanged.AddListener(isOn =>
            {
                //切换显隐
                if (isOn)
                {
                    if (IsAsync) _ = ui.ShowAsync(ui.destroyCancellationToken);
                    else ui.Show();
                }
                else
                {
                    if (IsAsync) _ = ui.HideAsync(ui.destroyCancellationToken);
                    else ui.Hide();
                }
            });
        }
    }
}
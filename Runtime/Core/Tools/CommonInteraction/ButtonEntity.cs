using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace UIManager
{
    /// <summary>
    ///   按钮的显隐交互实体类
    /// </summary>
    [Serializable]
    public class ButtonEntity : ShowHideInteractionEntity
    {
        [field: SerializeField, Header("显隐行为")]
        private ShowHideAction action;

        [SerializeField, Header("显隐按钮")] private Button button;

        /// <summary>
        ///  显隐行为
        /// </summary>
        public override ShowHideAction Action => action;

        public override void Init(UIBase ui)
        {
            button.onClick.AddListener(() =>
            {
                //如果是显示
                if (Action == ShowHideAction.ToShow)
                {
                    if (IsAsync) ui.ShowAsync(ui.destroyCancellationToken).Forget();
                    else ui.Show();
                    return;
                }

                if (Action == ShowHideAction.ToHide)
                {
                    if (IsAsync) ui.HideAsync(ui.destroyCancellationToken).Forget();
                    else ui.Hide();
                    return;
                }

                if (Action == ShowHideAction.Switch)
                {
                    if (ui.State is UIState.Showed or UIState.Showing)
                    {
                        if (IsAsync) ui.HideAsync(ui.destroyCancellationToken).Forget();
                        else ui.Hide();
                    }
                    else
                    {
                        if (IsAsync) ui.ShowAsync(ui.destroyCancellationToken).Forget();
                        else ui.Show();
                    }
                }
            });
        }
    }
}
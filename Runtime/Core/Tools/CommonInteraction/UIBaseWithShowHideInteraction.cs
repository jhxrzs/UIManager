using System.Collections.Generic;
using UnityEngine;

namespace UIManager
{
    /// <summary>
    ///  显示和隐藏的交互
    /// </summary>
    public class UIBaseWithShowHideInteraction : UIBase
    {
        /// <summary>
        ///  按钮的显隐交互
        /// </summary>
        [SerializeField] private List<ButtonEntity> buttonInteraction;
        
        /// <summary>
        ///  toggle的显隐交互
        /// </summary>
        [ SerializeField] private List<ToggleEntity> toggleInteraction;

        protected override void Start()
        {
            base.Start();
            
            //初始化按钮的显隐交互
            foreach (var buttonEntity in buttonInteraction) buttonEntity.Init(this);
            
            // 初始化toggle的显隐交互
            foreach (var toggleEntity in toggleInteraction) toggleEntity.Init(this);

        }
        
    }
}
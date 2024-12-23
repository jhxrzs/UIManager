using System;
using UnityEngine;

namespace UIManager
{
    /// <summary>
    ///  显示隐藏的交互实体类
    /// </summary>
    [Serializable]
    public abstract class ShowHideInteractionEntity
    {
        /// <summary>
        ///  显隐行为
        /// </summary>
        public abstract ShowHideAction Action { get; }

        /// <summary>
        ///  是否异步
        /// </summary>
        [field: SerializeField]
        public bool IsAsync { get; set; }

        /// <summary>
        ///  初始化ui的显隐交互
        /// </summary>
        public abstract void Init(UIBase ui);
    }
}
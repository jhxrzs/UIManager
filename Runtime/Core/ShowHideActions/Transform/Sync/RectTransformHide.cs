using UnityEngine;

namespace JH
{
    /// <summary>
    ///  利用RectTransform的DoTween动画来隐藏UI的同步效果
    /// </summary>
    [RequireComponent(typeof(UIBase))]
    public class RectTransformHide : RectTransformShow
    {
        /// <summary>
        ///  激活脚本时，监听UI面板的同步显示事件
        /// </summary>
        protected override void Awake()
        {
            uiBase.HideActions += SetRectTransform;
        }

        /// <summary>
        ///  禁用脚本时，取消监听UI面板的同步显示事件
        /// </summary>
        protected override void OnDestroy()
        {
            if (uiBase != null) uiBase.HideActions -= SetRectTransform;
        }
    }
}
using UnityEngine;

namespace UIManager
{  /// <summary>
    ///  利用RectTransform的DoTween动画来隐藏UI的异步效果
    /// </summary>
    [RequireComponent(typeof(UIBase))]
    public class RectTransformTweenHide :RectTransformTweenShow
    {
        /// <summary>
        ///  激活脚本时，监听UI面板的异步显示事件
        /// </summary>
        protected override void Awake()
        {
            uiBase.HideAsyncActions += TweenRectTransformAsync;
        }

        /// <summary>
        ///  禁用脚本时，取消监听UI面板的异步显示事件
        /// </summary>
        protected override void OnDestroy()
        {
            if (uiBase != null) uiBase.HideAsyncActions -= TweenRectTransformAsync;
        }
    }
}
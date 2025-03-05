using UnityEngine;

namespace JH
{
    /// <summary>
    /// 逐渐隐藏的UI动画
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(UIBase))]
    public class FadeOut : FadeIn
    {
        /// <summary>
        ///  目标透明度
        /// </summary>
        protected override float EndAlpha => 0;

        /// <summary>
        ///  激活脚本时，监听UI面板的异步隐藏事件，加入FadeOut效果的动画
        /// </summary>
        protected override void Awake()
        {
            uiBase.HideAsyncActions += TweenAlphaAsync;
        }
        
        /// <summary>
        ///  禁用脚本时，取消监听UI面板的异步隐藏事件
        /// </summary>
        protected override void OnDestroy()
        {
            if(uiBase != null)   uiBase.HideAsyncActions -= TweenAlphaAsync;
        }
        
    }
}
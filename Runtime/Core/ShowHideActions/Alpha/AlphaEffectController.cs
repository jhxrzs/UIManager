using UnityEngine;

namespace JH
{
    /// <summary>
    ///  透明度特效控制器
    /// </summary>
    public class AlphaEffectController : EffectControllerBase
    {
        /// <summary>将隐藏进度转换为面板整体透明度。</summary>
        /// <param name="value">零表示完全显示，一表示完全隐藏的动效进度。</param>
        protected override void OnEffectAmountChanged(float value)
        {
            // 保留基类进度通知，再将隐藏进度反向映射为可见度。
            base.OnEffectAmountChanged(value);
            // 将隐藏进度反向钳制为 CanvasGroup 的整体透明度。
            CanvasGroup.alpha = Mathf.Clamp01(1 - value);
        }
    }
}

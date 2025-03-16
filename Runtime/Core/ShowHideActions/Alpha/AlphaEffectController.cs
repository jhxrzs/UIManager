using UnityEngine;

namespace JH
{
    /// <summary>
    ///  透明度特效控制器
    /// </summary>
    public class AlphaEffectController : EffectControllerBase
    {
        protected override void OnEffectAmountChanged(float value)
        {
            base.OnEffectAmountChanged(value);
            CanvasGroup.alpha = Mathf.Clamp01(1 - value);
        }
    }
}
using UnityEngine;
using UnityEngine.UI;

namespace JH
{
    /// <summary>
    ///  通过调整Graphic色彩值控制UI显示和隐藏的色彩特效控制器
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class ColorEffectController : EffectControllerBase
    {
        /// <summary>
        ///  UI完全显示时的色彩值
        /// </summary>
        [SerializeField, Tooltip("UI完全显示时的色彩值")]
        private Color showColor = Color.white;

        /// <summary>
        ///  UI完全隐藏时的色彩值
        /// </summary>
        [SerializeField, Tooltip("UI完全隐藏时的色彩值")]
        private Color hideColor = Color.clear;

        /// <summary>
        ///  应用色彩特效的Graphic组件
        /// </summary>
        [SerializeField, HideInInspector] private Graphic graphic;

        /// <summary>
        ///  获取应用色彩特效的Graphic组件
        /// </summary>
        private Graphic Graphic => graphic ?? GetComponent<Graphic>();

        /// <summary>
        ///  当动效进度改变时，在显示色彩值和隐藏色彩值之间进行插值
        /// </summary>
        /// <param name="value">动效进度,0==显示，1==隐藏</param>
        protected override void OnEffectAmountChanged(float value)
        {
            // 保留动效控制基类的进度变化处理
            base.OnEffectAmountChanged(value);

            // 根据动效进度计算并应用当前Graphic色彩值
            Graphic.color = Color.Lerp(showColor, hideColor, value);
        }

        /// <summary>
        ///  检视面板中的配置发生变化时，同步Graphic组件和当前色彩值
        /// </summary>
        protected override void OnValidate()
        {
            // 缓存当前对象上的Graphic组件
            graphic = GetComponent<Graphic>();

            // 同步动效控制基类的组件引用和当前动效进度
            base.OnValidate();
        }
    }
}

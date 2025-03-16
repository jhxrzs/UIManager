using System;
using UnityEngine;

namespace JH
{
    /// <summary>
    ///  用于UI动效的RectTransform序列化类
    /// </summary>
    [Serializable]
    public struct EffectSerializedRectTransform
    {
        public Vector2 anchorPos;
        public Vector3 angle;
        public Vector3 scale;
        public Vector2 sizeDelta;
        public Vector2 pivot;

        public bool Equals(EffectSerializedRectTransform other)
        {
            return anchorPos.Equals(other.anchorPos) && angle.Equals(other.angle) && scale.Equals(other.scale) &&
                   sizeDelta.Equals(other.sizeDelta) && pivot.Equals(other.pivot);
        }
        
        /// <summary>
        /// 创建实例
        /// </summary>
        public static EffectSerializedRectTransform GetInstance(RectTransform rectTransform)
        {
            return new EffectSerializedRectTransform
            {
                anchorPos = rectTransform.anchoredPosition,
                angle = rectTransform.localEulerAngles,
                scale = rectTransform.localScale,
                sizeDelta = rectTransform.sizeDelta,
                pivot = rectTransform.pivot
            };
        }

        /// <summary>
        ///  应用到RectTransform
        /// </summary>
        public void Apply(RectTransform rectTransform)
        {
            rectTransform.anchoredPosition = anchorPos;
            rectTransform.localEulerAngles = angle;
            rectTransform.localScale = scale;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.pivot = pivot;
        }

        /// <summary>
        ///   插值
        /// </summary>
        public static EffectSerializedRectTransform Lerp(
            EffectSerializedRectTransform from, EffectSerializedRectTransform to, float t,
            Ease posEase, Ease angleEase, Ease scaleEase, Ease sizeDeltaEase, Ease pivotEase)
        {
            return new EffectSerializedRectTransform
            {
                anchorPos = EaseFunctionNumeric.Tween(from.anchorPos, to.anchorPos, posEase, t),
                angle = EaseFunctionNumeric.Tween(from.angle, to.angle, angleEase, t),
                scale = EaseFunctionNumeric.Tween(from.scale, to.scale, scaleEase, t),
                sizeDelta = EaseFunctionNumeric.Tween(from.sizeDelta, to.sizeDelta, sizeDeltaEase, t),
                pivot = EaseFunctionNumeric.Tween(from.pivot, to.pivot, pivotEase, t)
            };
        }
    }
}
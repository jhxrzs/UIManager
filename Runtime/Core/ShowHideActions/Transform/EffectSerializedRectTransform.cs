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
                anchorPos = Vector2.LerpUnclamped(from.anchorPos, to.anchorPos, UIEasing.Evaluate(posEase, t)),
                angle = Vector3.LerpUnclamped(from.angle, to.angle, UIEasing.Evaluate(angleEase, t)),
                scale = Vector3.LerpUnclamped(from.scale, to.scale, UIEasing.Evaluate(scaleEase, t)),
                sizeDelta = Vector2.LerpUnclamped(from.sizeDelta, to.sizeDelta, UIEasing.Evaluate(sizeDeltaEase, t)),
                pivot = Vector2.LerpUnclamped(from.pivot, to.pivot, UIEasing.Evaluate(pivotEase, t))
            };
        }

        /// <summary>使用已经求值的全局曲线进度，保留曲线过冲，不再次求曲线或钳制。</summary>
        internal static EffectSerializedRectTransform LerpUnclamped(
            EffectSerializedRectTransform from, EffectSerializedRectTransform to, float progress)
        {
            return new EffectSerializedRectTransform
            {
                anchorPos = Vector2.LerpUnclamped(from.anchorPos, to.anchorPos, progress),
                angle = Vector3.LerpUnclamped(from.angle, to.angle, progress),
                scale = Vector3.LerpUnclamped(from.scale, to.scale, progress),
                sizeDelta = Vector2.LerpUnclamped(from.sizeDelta, to.sizeDelta, progress),
                pivot = Vector2.LerpUnclamped(from.pivot, to.pivot, progress)
            };
        }
    }
}

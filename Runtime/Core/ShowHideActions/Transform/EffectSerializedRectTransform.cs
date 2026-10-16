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
        /// <summary>矩形相对于锚点的二维位置。</summary>
        public Vector2 anchorPos;
        /// <summary>保留配置整圈数与符号的本地欧拉角。</summary>
        public Vector3 angle;
        /// <summary>矩形相对于父节点的本地缩放。</summary>
        public Vector3 scale;
        /// <summary>矩形尺寸相对锚点范围的增量。</summary>
        public Vector2 sizeDelta;
        /// <summary>矩形内部用于旋转与缩放的归一化轴心。</summary>
        public Vector2 pivot;

        /// <summary>逐项比较两个矩形姿态保存的位置、旋转、缩放、大小与轴心。</summary>
        /// <param name="other">参与逐项比较的另一份矩形姿态。</param>
        /// <returns>五项姿态值全部相等时为真。</returns>
        public bool Equals(EffectSerializedRectTransform other)
        {
            // 所有姿态分量都相等时才视为同一份配置。
            return anchorPos.Equals(other.anchorPos) && angle.Equals(other.angle) && scale.Equals(other.scale) &&
                   sizeDelta.Equals(other.sizeDelta) && pivot.Equals(other.pivot);
        }
        
        /// <summary>
        /// 创建实例
        /// </summary>
        /// <param name="rectTransform">读取或写入姿态的目标矩形组件。</param>
        /// <returns>保存目标矩形当前姿态的新值。</returns>
        public static EffectSerializedRectTransform GetInstance(RectTransform rectTransform)
        {
            // 逐项读取矩形当前值，返回不依赖原组件的姿态快照。
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
        /// <param name="rectTransform">读取或写入姿态的目标矩形组件。</param>
        public void Apply(RectTransform rectTransform)
        {
            // 恢复相对锚点的位置。
            rectTransform.anchoredPosition = anchorPos;
            // 写入配置的欧拉角，保留动画计算时的角度分支。
            rectTransform.localEulerAngles = angle;
            // 恢复相对于父节点的缩放。
            rectTransform.localScale = scale;
            // 恢复相对锚点范围的尺寸增量。
            rectTransform.sizeDelta = sizeDelta;
            // 恢复归一化轴心位置。
            rectTransform.pivot = pivot;
        }

        /// <summary>
        ///   插值
        /// </summary>
        /// <param name="from">插值开始端的矩形姿态。</param>
        /// <param name="to">插值结束端的矩形姿态。</param>
        /// <param name="t">用于各属性曲线求值的零到一时间进度。</param>
        /// <param name="posEase">锚点位置使用的缓动曲线。</param>
        /// <param name="angleEase">本地欧拉角使用的缓动曲线。</param>
        /// <param name="scaleEase">本地缩放使用的缓动曲线。</param>
        /// <param name="sizeDeltaEase">矩形尺寸增量使用的缓动曲线。</param>
        /// <param name="pivotEase">轴心使用的缓动曲线。</param>
        /// <returns>分别应用各属性曲线后得到的矩形姿态。</returns>
        public static EffectSerializedRectTransform Lerp(
            EffectSerializedRectTransform from, EffectSerializedRectTransform to, float t,
            Ease posEase, Ease angleEase, Ease scaleEase, Ease sizeDeltaEase, Ease pivotEase)
        {
            // 分别求值五条属性曲线，再对相应姿态分量插值。
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
        /// <param name="from">插值开始端的矩形姿态。</param>
        /// <param name="to">插值结束端的矩形姿态。</param>
        /// <param name="progress">已经求值、允许超出零到一范围的全局插值权重。</param>
        /// <returns>保留给定全局进度过冲的矩形姿态。</returns>
        internal static EffectSerializedRectTransform LerpUnclamped(
            EffectSerializedRectTransform from, EffectSerializedRectTransform to, float progress)
        {
            // 所有属性共用已求值的全局权重，不重复求曲线或限制过冲。
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

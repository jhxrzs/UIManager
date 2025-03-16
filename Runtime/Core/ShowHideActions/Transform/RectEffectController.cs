using UnityEngine;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace JH
{
    /// <summary>
    ///  利用RectTransform的DoTween动画来显示隐藏UI的异步效果
    /// </summary>
    public class RectEffectController : EffectControllerBase
    {
        #region 显示配置

        /// <summary>
        ///  显示时的目标位置
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("显示")]
#endif
        [SerializeField]
        private EffectSerializedRectTransform showTarget;

        /// <summary>
        ///  位置动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("显示")]
        [Tooltip("当showEase不是未设置状态时，此字段无效")]
        [EnableIf("showEase", Ease.NotSet)]
#endif
        [SerializeField]
        protected Ease showAnchorPosEase = Ease.Linear;

        /// <summary>
        ///  旋转动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("显示")]
        [Tooltip("当showEase不是未设置状态时，此字段无效")]
        [EnableIf("showEase", Ease.NotSet)]
#endif
        [SerializeField]
        protected Ease showAngleEase = Ease.Linear;

        /// <summary>
        ///  缩放动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("显示")]
        [Tooltip("当showEase不是未设置状态时，此字段无效")]
        [EnableIf("showEase", Ease.NotSet)]
#endif
        [SerializeField]
        protected Ease showScaleEase = Ease.Linear;

        /// <summary>
        ///  大小动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("显示")]
        [Tooltip("当showEase不是未设置状态时，此字段无效")]
        [EnableIf("showEase", Ease.NotSet)]
#endif
        [SerializeField]
        protected Ease showSizeDeltaEase = Ease.Linear;

        /// <summary>
        ///   轴心动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("显示")]
        [Tooltip("当showEase不是未设置状态时，此字段无效")]
        [EnableIf("showEase", Ease.NotSet)]
#endif
        [SerializeField]
        protected Ease showPivotEase = Ease.Linear;

#if ODIN_INSPECTOR
        [Button("记录显示位置")]
#endif
        [ContextMenu("记录显示位置")]
        private void RecordShowTarget()
        {
            showTarget = EffectSerializedRectTransform.GetInstance(transform as RectTransform);
        }

        #endregion

        #region 隐藏配置

        /// <summary>
        ///  隐藏时的目标位置
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("隐藏")]
#endif
        [SerializeField]
        private EffectSerializedRectTransform hideTarget;

        /// <summary>
        ///  位置动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("隐藏")]
        [Tooltip("当hideEase不是未设置状态时，此字段无效")]
        [EnableIf("hideEase", Ease.NotSet)]
#endif
        [SerializeField]
        protected Ease hideAnchorPosEase = Ease.Linear;

        /// <summary>
        ///  旋转动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("隐藏")]
        [Tooltip("当hideEase不是未设置状态时，此字段无效")]
        [EnableIf("hideEase", Ease.NotSet)]
#endif
        [SerializeField]
        protected Ease hideAngleEase = Ease.Linear;

        /// <summary>
        ///  缩放动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("隐藏")]
        [Tooltip("当hideEase不是未设置状态时，此字段无效")]
        [EnableIf("hideEase", Ease.NotSet)]
#endif
        [SerializeField]
        protected Ease hideScaleEase = Ease.Linear;

        /// <summary>
        ///  大小动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("隐藏")]
        [Tooltip("当hideEase不是未设置状态时，此字段无效")]
        [EnableIf("hideEase", Ease.NotSet)]
#endif
        [SerializeField]
        protected Ease hideSizeDeltaEase = Ease.Linear;

        /// <summary>
        ///   轴心动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("隐藏")]
        [Tooltip("当hideEase不是未设置状态时，此字段无效")]
        [EnableIf("hideEase", Ease.NotSet)]
#endif
        [SerializeField]
        protected Ease hidePivotEase = Ease.Linear;

#if ODIN_INSPECTOR
        [Button("记录隐藏位置")]
#endif
        [ContextMenu("记录隐藏位置")]
        private void RecordHideTarget()
        {
            hideTarget = EffectSerializedRectTransform.GetInstance(transform as RectTransform);
        }

        #endregion

        /// <summary>
        ///  重写此方法，用于检查是否有未赋值的字段，如果有则不进行编辑器下的特效值同步，这会导致RectTransform的值被清空
        ///  同时，如果showEase和hideEase如果不是未设置状态，则统一赋值给showAnchorPosEase等
        /// </summary>
        protected override void OnValidate()
        {
            uiBase = GetComponent<UIBase>();
            canvasGroup = GetComponent<CanvasGroup>();
            //如果任意一个为空，则不进行编辑器下的特效值同步，这会导致RectTransform的值被清空
            if (!showTarget.Equals(default) && !hideTarget.Equals(default))
            {
                EffectAmount = effectAmount;
            }

            // 如果showEase和hideEase不是未设置状态，则统一赋值给showAnchorPosEase等
            if (showEase != Ease.NotSet)
                showAnchorPosEase = showAngleEase = showScaleEase = showSizeDeltaEase = showPivotEase = showEase;
            if (hideEase != Ease.NotSet)
                hideAnchorPosEase = hideAngleEase = hideScaleEase = hideSizeDeltaEase = hidePivotEase = hideEase;
        }

        protected override void OnEffectAmountChanged(float value)
        {
            base.OnEffectAmountChanged(value);

            // 根据特效进度进行插值
            var current = EffectSerializedRectTransform.Lerp(
                showTarget, hideTarget, value,
                showAnchorPosEase, showAngleEase, showScaleEase, showSizeDeltaEase, showPivotEase);

            // 应用到RectTransform
            current.Apply(transform as RectTransform);
        }
    }
}
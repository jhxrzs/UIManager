using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace JH
{
    /// <summary>
    ///  通过RectTransform的位置、旋转、缩放、大小和轴心过渡实现显隐效果。
    /// </summary>
    public class RectEffectController : EffectControllerBase
    {
        private EffectSerializedRectTransform _transitionFrom;
        private float _transitionFromAmount;
        private bool _isShowing;
        private bool _isTransitioning;
        private int _transitionVersion;

        #region 显示配置

        /// <summary>
        ///  显示时的目标位置
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("显示")]
#endif
        [SerializeField] private EffectSerializedRectTransform showTarget;

        /// <summary>
        ///  位置动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("显示")]
        [Tooltip("当showEase不是未设置状态时，此字段无效")]
        [EnableIf("showEase", Ease.NotSet)]
#endif
        [SerializeField] protected Ease showAnchorPosEase = Ease.Linear;

        /// <summary>
        ///  旋转动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("显示")]
        [Tooltip("当showEase不是未设置状态时，此字段无效")]
        [EnableIf("showEase", Ease.NotSet)]
#endif
        [SerializeField] protected Ease showAngleEase = Ease.Linear;

        /// <summary>
        ///  缩放动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("显示")]
        [Tooltip("当showEase不是未设置状态时，此字段无效")]
        [EnableIf("showEase", Ease.NotSet)]
#endif
        [SerializeField] protected Ease showScaleEase = Ease.Linear;

        /// <summary>
        ///  大小动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("显示")]
        [Tooltip("当showEase不是未设置状态时，此字段无效")]
        [EnableIf("showEase", Ease.NotSet)]
#endif
        [SerializeField] protected Ease showSizeDeltaEase = Ease.Linear;

        /// <summary>
        ///   轴心动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("显示")]
        [Tooltip("当showEase不是未设置状态时，此字段无效")]
        [EnableIf("showEase", Ease.NotSet)]
#endif
        [SerializeField] protected Ease showPivotEase = Ease.Linear;

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
        [SerializeField] private EffectSerializedRectTransform hideTarget;

        /// <summary>
        ///  位置动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("隐藏")]
        [Tooltip("当hideEase不是未设置状态时，此字段无效")]
        [EnableIf("hideEase", Ease.NotSet)]
#endif
        [SerializeField] protected Ease hideAnchorPosEase = Ease.Linear;

        /// <summary>
        ///  旋转动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("隐藏")]
        [Tooltip("当hideEase不是未设置状态时，此字段无效")]
        [EnableIf("hideEase", Ease.NotSet)]
#endif
        [SerializeField] protected Ease hideAngleEase = Ease.Linear;

        /// <summary>
        ///  缩放动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("隐藏")]
        [Tooltip("当hideEase不是未设置状态时，此字段无效")]
        [EnableIf("hideEase", Ease.NotSet)]
#endif
        [SerializeField] protected Ease hideScaleEase = Ease.Linear;

        /// <summary>
        ///  大小动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("隐藏")]
        [Tooltip("当hideEase不是未设置状态时，此字段无效")]
        [EnableIf("hideEase", Ease.NotSet)]
#endif
        [SerializeField] protected Ease hideSizeDeltaEase = Ease.Linear;

        /// <summary>
        ///   轴心动画曲线
        /// </summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("隐藏")]
        [Tooltip("当hideEase不是未设置状态时，此字段无效")]
        [EnableIf("hideEase", Ease.NotSet)]
#endif
        [SerializeField] protected Ease hidePivotEase = Ease.Linear;

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
        ///  缓存组件并预览当前进度；全局曲线覆盖分项曲线，但不改写其配置。
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
        }

        protected override void OnUIShow()
        {
            EndTransition();
            base.OnUIShow();
        }

        protected override void OnUIHide()
        {
            EndTransition();
            base.OnUIHide();
        }

        protected override UniTask OnUIShowAsync(CancellationToken token) => RunTransitionAsync(true, token);

        protected override UniTask OnUIHideAsync(CancellationToken token) => RunTransitionAsync(false, token);

        private async UniTask RunTransitionAsync(bool isShowing, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            // 反向播放及取消后重试均从当前实际姿态开始，不将进度直接套到另一条曲线上。
            _transitionFrom = EffectSerializedRectTransform.GetInstance(transform as RectTransform);
            _transitionFromAmount = EffectAmount;
            _isShowing = isShowing;
            _isTransitioning = true;
            var version = ++_transitionVersion;
            try
            {
                // 基类继续负责时长、全局曲线、进度、交互开关以及取消检查。
                if (isShowing) await base.OnUIShowAsync(token);
                else await base.OnUIHideAsync(token);
            }
            finally
            {
                // 旧动画的取消收尾不得清除后来启动的新动画。
                if (_transitionVersion == version) _isTransitioning = false;
            }
        }

        private void EndTransition()
        {
            _transitionVersion++;
            _isTransitioning = false;
        }

        protected override void OnEffectAmountChanged(float value)
        {
            base.OnEffectAmountChanged(value);

            if (_isTransitioning)
            {
                var targetAmount = _isShowing ? 0f : 1f;
                var amountRange = targetAmount - _transitionFromAmount;
                var progress = Mathf.Approximately(amountRange, 0f)
                    ? 1f
                    : (value - _transitionFromAmount) / amountRange;
                // 全局曲线已由基类应用到进度，仅在 NotSet 时再使用对应方向的分项曲线。
                var useParts = (_isShowing ? showEase : hideEase) == Ease.NotSet;
                var animated = EffectSerializedRectTransform.Lerp(
                    _transitionFrom, _isShowing ? showTarget : hideTarget, progress,
                    useParts ? (_isShowing ? showAnchorPosEase : hideAnchorPosEase) : Ease.Linear,
                    useParts ? (_isShowing ? showAngleEase : hideAngleEase) : Ease.Linear,
                    useParts ? (_isShowing ? showScaleEase : hideScaleEase) : Ease.Linear,
                    useParts ? (_isShowing ? showSizeDeltaEase : hideSizeDeltaEase) : Ease.Linear,
                    useParts ? (_isShowing ? showPivotEase : hidePivotEase) : Ease.Linear);
                animated.Apply(transform as RectTransform);
                return;
            }

            // 静态预览及同步显隐按显示端到隐藏端的进度取值，不依赖上一轮动画方向。
            var current = EffectSerializedRectTransform.Lerp(
                showTarget, hideTarget, value,
                Ease.Linear, Ease.Linear, Ease.Linear, Ease.Linear, Ease.Linear);

            // 应用到RectTransform
            current.Apply(transform as RectTransform);
        }
    }
}

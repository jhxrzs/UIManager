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
        /// <summary>本轮动画从实际矩形读取的起始姿态。</summary>
        private EffectSerializedRectTransform _transitionFrom;
        /// <summary>开始本轮动画时已应用的归一化隐藏进度。</summary>
        private float _transitionFromAmount;
        /// <summary>本轮动画独立的零到一线性时间进度。</summary>
        private float _transitionProgress;
        /// <summary>应用全局曲线后允许过冲的本轮动画进度。</summary>
        private float _transitionEasedProgress;
        /// <summary>本轮使用的全局曲线，未设置时分别使用各属性曲线。</summary>
        private Ease _transitionGlobalEase;
        /// <summary>当前动画是否朝显示目标推进。</summary>
        private bool _isShowing;
        /// <summary>是否正在应用从实际姿态开始的异步过渡。</summary>
        private bool _isTransitioning;
        /// <summary>用于阻止旧动画回调和收尾改写新动画的递增版本。</summary>
        private int _transitionVersion;
        /// <summary>上次实际写入矩形的未归一化欧拉角。</summary>
        private Vector3 _lastAppliedAngle;
        /// <summary>是否已有可用于恢复旋转分支的历史写入角度。</summary>
        private bool _hasAppliedAngle;

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

        /// <summary>把当前矩形姿态记录为显示目标。</summary>
#if ODIN_INSPECTOR
        [Button("记录显示位置")]
#endif
        [ContextMenu("记录显示位置")]
        private void RecordShowTarget()
        {
            // 读取当前 RectTransform 并替换显示端配置。
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

        /// <summary>把当前矩形姿态记录为隐藏目标。</summary>
#if ODIN_INSPECTOR
        [Button("记录隐藏位置")]
#endif
        [ContextMenu("记录隐藏位置")]
        private void RecordHideTarget()
        {
            // 读取当前 RectTransform 并替换隐藏端配置。
            hideTarget = EffectSerializedRectTransform.GetInstance(transform as RectTransform);
        }

        #endregion

        /// <summary>
        ///  重写此方法，用于检查是否有未赋值的字段，如果有则不进行编辑器下的特效值同步，这会导致RectTransform的值被清空
        ///  缓存组件并预览当前进度；全局曲线覆盖分项曲线，但不改写其配置。
        /// </summary>
        protected override void OnValidate()
        {
            // 刷新面板引用以支持编辑器中即时预览。
            uiBase = GetComponent<UIBase>();
            // 刷新当前节点上的透明度容器引用。
            canvasGroup = GetComponent<CanvasGroup>();
            //如果任意一个为空，则不进行编辑器下的特效值同步，这会导致RectTransform的值被清空
            if (!showTarget.Equals(default) && !hideTarget.Equals(default))
            {
                // 两端姿态均有效时才应用序列化进度，避免默认值清空矩形。
                EffectAmount = effectAmount;
            }
        }

        /// <summary>结束旧异步过渡并立即应用显示端姿态。</summary>
        protected override void OnUIShow()
        {
            // 使旧过渡回调失效，切回同步姿态模式。
            EndTransition();
            // 由基类设置显示端进度并触发统一姿态更新。
            base.OnUIShow();
        }

        /// <summary>结束旧异步过渡并立即应用隐藏端姿态。</summary>
        protected override void OnUIHide()
        {
            // 同步隐藏前先结束旧的异步姿态插值。
            EndTransition();
            // 由基类设置隐藏端进度并触发统一姿态更新。
            base.OnUIHide();
        }

        /// <summary>从当前实际姿态异步过渡到显示目标。</summary>
        /// <param name="token">取消当前显隐或姿态过渡的令牌。</param>
        /// <returns>显示过渡完成、取消或失败时结束的任务。</returns>
        protected override UniTask OnUIShowAsync(CancellationToken token) => RunTransitionAsync(true, token);

        /// <summary>从当前实际姿态异步过渡到隐藏目标。</summary>
        /// <param name="token">取消当前显隐或姿态过渡的令牌。</param>
        /// <returns>隐藏过渡完成、取消或失败时结束的任务。</returns>
        protected override UniTask OnUIHideAsync(CancellationToken token) => RunTransitionAsync(false, token);

        /// <summary>捕获当前姿态并以独立时间进度推进矩形各属性。</summary>
        /// <param name="isShowing">是否向显示目标过渡。</param>
        /// <param name="token">取消当前显隐或姿态过渡的令牌。</param>
        /// <returns>本轮矩形姿态过渡的异步任务。</returns>
        private async UniTask RunTransitionAsync(bool isShowing, CancellationToken token)
        {
            // 取消已发生时不采样或改写当前矩形。
            token.ThrowIfCancellationRequested();
            // 反向播放及取消后重试均从当前实际姿态开始，不将进度直接套到另一条曲线上。
            var rect = transform as RectTransform;
            // 捕获本轮真实起始姿态，使反向播放从当前位置连续开始。
            _transitionFrom = EffectSerializedRectTransform.GetInstance(rect);
            // Unity 读回的欧拉角会把负角度和整圈数归一化。保留本控制器上次写入的角度分支，
            // 让过冲反向、取消重试和有意配置的多圈路径连续；外部改变旋转时仍以实际姿态为准。
            if (_hasAppliedAngle &&
                Quaternion.Angle(rect.localRotation, Quaternion.Euler(_lastAppliedAngle)) < 0.01f)
                // 旋转未被外部修改时沿用上次写入的整圈与符号信息。
                _transitionFrom.angle = _lastAppliedAngle;
            // 保存当前隐藏进度，供本轮动画持续更新基类进度。
            _transitionFromAmount = EffectAmount;
            // 按显示或隐藏方向选择本轮全局曲线。
            _transitionGlobalEase = isShowing ? showEase : hideEase;
            // 记录目标方向，后续每帧据此选择目标姿态。
            _isShowing = isShowing;
            // 进入以实际姿态为起点的异步过渡模式。
            _isTransitioning = true;
            // 递增版本，使之前动画的回调与收尾失效。
            var version = ++_transitionVersion;
            // 保证动画退出时只清理仍属于本轮的过渡标记。
            try
            {
                // 使用独立的时间进度，不能从已钳制的 EffectAmount 反推曲线过冲。
                await UITransition.RunAsync(0f, 1f, progress =>
                {
                    // 仅当前版本可以将时间进度写入矩形表现。
                    if (_transitionVersion == version) ApplyTransitionProgress(progress);
                }, isShowing ? ShowDuration : HideDuration, token, Ease.Linear);
            }
            finally
            {
                // 旧动画的取消收尾不得清除后来启动的新动画。
                if (_transitionVersion == version) _isTransitioning = false;
            }
        }

        /// <summary>记录本轮线性与缓动进度，再通知基类更新动效。</summary>
        /// <param name="progress">本轮动画独立的零到一线性时间进度。</param>
        private void ApplyTransitionProgress(float progress)
        {
            // 保存限制后的线性时间，供各属性独立求曲线。
            _transitionProgress = Mathf.Clamp01(progress);
            // 配置全局曲线时统一求值，否则保留线性时间供分项曲线使用。
            _transitionEasedProgress = _transitionGlobalEase == Ease.NotSet
                ? _transitionProgress
                : UIEasing.Evaluate(_transitionGlobalEase, _transitionProgress);
            // 保留归一化进度及虚方法通知；实际姿态使用独立的曲线进度。
            EffectAmount = Mathf.LerpUnclamped(
                _transitionFromAmount, _isShowing ? 0f : 1f, _transitionEasedProgress);
        }

        /// <summary>使旧动画回调失效并退出异步姿态插值模式。</summary>
        private void EndTransition()
        {
            // 递增版本，阻止迟到的异步帧继续写入姿态。
            _transitionVersion++;
            // 后续进度通知恢复静态或同步显隐的插值方式。
            _isTransitioning = false;
        }

        /// <summary>根据当前动画模式选取属性曲线并应用矩形姿态。</summary>
        /// <param name="value">零表示显示、一表示隐藏的归一化动效进度。</param>
        protected override void OnEffectAmountChanged(float value)
        {
            // 保留基类的进度变化扩展入口。
            base.OnEffectAmountChanged(value);

            // 异步过程中从本轮实际起始姿态插值，而非固定显示端。
            if (_isTransitioning)
            {
                // 按当前动画方向选择显示或隐藏目标。
                var target = _isShowing ? showTarget : hideTarget;
                // 全局曲线优先；未设置时分别求位置、角度、缩放、尺寸和轴心曲线。
                var animated = _transitionGlobalEase == Ease.NotSet
                    ? EffectSerializedRectTransform.Lerp(
                        _transitionFrom, target, _transitionProgress,
                        _isShowing ? showAnchorPosEase : hideAnchorPosEase,
                        _isShowing ? showAngleEase : hideAngleEase,
                        _isShowing ? showScaleEase : hideScaleEase,
                        _isShowing ? showSizeDeltaEase : hideSizeDeltaEase,
                        _isShowing ? showPivotEase : hidePivotEase)
                    : EffectSerializedRectTransform.LerpUnclamped(_transitionFrom, target, _transitionEasedProgress);
                // 将本帧异步姿态写入矩形并缓存角度分支。
                ApplyPose(animated);
                // 已应用异步姿态后不再重复执行静态预览插值。
                return;
            }

            // 静态预览及同步显隐按显示端到隐藏端的进度取值，不依赖上一轮动画方向。
            var current = EffectSerializedRectTransform.Lerp(
                showTarget, hideTarget, value,
                Ease.Linear, Ease.Linear, Ease.Linear, Ease.Linear, Ease.Linear);

            // 应用到RectTransform
            ApplyPose(current);
        }

        /// <summary>写入矩形姿态并保存未经欧拉角归一化的旋转分支。</summary>
        /// <param name="pose">需要写入当前矩形的完整姿态。</param>
        private void ApplyPose(EffectSerializedRectTransform pose)
        {
            // 把五项姿态值统一写入当前 RectTransform。
            pose.Apply(transform as RectTransform);
            // 记录实际发出的未归一化值，取消不清空；不能把目标改成最短角，否则会丢失多圈动画。
            _lastAppliedAngle = pose.angle;
            // 标记角度缓存有效，供下一轮反向或取消重试保留连续性。
            _hasAppliedAngle = true;
        }
    }
}

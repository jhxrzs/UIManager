using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace JH
{
    /// <summary>
    ///  动效控制基类
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(UIBase))]
    public class EffectControllerBase : MonoBehaviour
    {
        /// <summary>组件销毁时取消的寿命令牌，提前缓存以避免销毁后访问原生属性。</summary>
        private CancellationToken _effectLifetimeToken;
        /// <summary>组件是否已经进入销毁流程，用于拒绝旧委托快照。</summary>
        private bool _isDestroyed;
        /// <summary>是否已进入 Start，允许在派生类初始化后补同步显隐状态。</summary>
        private bool _hasStarted;
        /// <summary>是否仍需为新挂载动效补同步一次稳定面板状态。</summary>
        private bool _initialPresentationPending = true;

        /// <summary>  
        ///  动效应用的的UI面板
        /// </summary>
        [SerializeField, HideInInspector] protected UIBase uiBase;

        /// <summary>
        ///  显示的动效时间
        /// </summary>
        [SerializeField, Tooltip("UI从隐藏到显示的用时")]
        private float showDuration = 1f;

        /// <summary>
        ///  隐藏的动效时间
        /// </summary>
        [SerializeField, Tooltip("UI从显示到隐藏的用时")]
        private float hideDuration = 1f;

        /// <summary>获取显示动画的持续秒数。</summary>
        protected float ShowDuration => showDuration;
        /// <summary>获取隐藏动画的持续秒数。</summary>
        protected float HideDuration => hideDuration;

        /// <summary>
        ///  UI从隐藏到显示的曲线
        /// </summary>
        [SerializeField, Tooltip("UI从隐藏到显示的曲线")]
        protected Ease showEase = Ease.NotSet;

        /// <summary>
        ///  UI从显示到隐藏的曲线
        /// </summary>
        [SerializeField, Tooltip("UI从显示到隐藏的曲线")]
        protected Ease hideEase = Ease.NotSet;

        /// <summary>
        ///  动效进度：0 == 显示，1 == 隐藏
        /// </summary>
        [SerializeField, Range(0, 1), Tooltip("动效进度：0 == 显示，1 == 隐藏")]
        protected float effectAmount;

        /// <summary>
        ///  CanvasGroup组件,用来控制UI面板的透明度
        /// </summary>
        [SerializeField, HideInInspector] protected CanvasGroup canvasGroup;

        /// <summary>
        ///  获取CanvasGroup组件
        /// </summary>
        protected CanvasGroup CanvasGroup
        {
            get
            {
                // Unity 已销毁对象也按缺失引用处理，补查结果写回缓存。
                if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
                // 缺少透明度容器时补建，使派生动效获得统一入口。
                if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
                // 返回当前仍有效的 CanvasGroup。
                return canvasGroup;
            }
        }

        /// <summary>
        ///  动效进度
        /// </summary>
        protected float EffectAmount
        {
            get => effectAmount;
            set
            {
                // 统一限制公开动效进度，派生类收到的值始终在零到一之间。
                effectAmount = Mathf.Clamp01(value);
                // 在保存进度后通知派生类更新具体表现。
                OnEffectAmountChanged(effectAmount);
            }
        }

        /// <summary>
        ///  激活脚本时，监听UI面板的异步显示事件，加入FadeIn效果的动画
        /// </summary>
        protected virtual void Awake()
        {
            // 保留编辑器已缓存的引用；运行时 AddComponent 或缺失缓存时只补查一次。
            if (!uiBase) uiBase = GetComponent<UIBase>();
            // 提前建立 CanvasGroup，避免首帧进度回调才补组件。
            _ = CanvasGroup;
            // 提前缓存；销毁后不得再读取 MonoBehaviour 的原生属性。
            _effectLifetimeToken = destroyCancellationToken;
            // 将异步显示纳入面板等待的动效行为集合。
            uiBase.ShowAsyncActions += ExecuteShowAsync;
            // 将异步隐藏纳入面板等待的动效行为集合。
            uiBase.HideAsyncActions += ExecuteHideAsync;
            // 同步显示时立即把动效推进到显示端。
            uiBase.ShowActions += ExecuteShow;
            // 同步隐藏时立即把动效推进到隐藏端。
            uiBase.HideActions += ExecuteHide;
            // 等待稳定状态，为本帧动态添加的动效补齐初始表现。
            uiBase.OnStateChanged += SynchronizeInitialPresentationAfterStateChange;
        }

        /// <summary>
        ///  等派生类 Awake 及调用方本帧配置结束后，补齐后挂载动效的稳定表现。
        /// </summary>
        protected virtual void Start()
        {
            // 记录派生类初始化已完成，可以开始读取稳定面板状态。
            _hasStarted = true;
            // 尝试应用面板已确定的初始显隐状态。
            TrySynchronizeInitialPresentation();
        }

        /// <summary>面板状态变化后尝试补齐新挂载动效的初始表现。</summary>
        /// <param name="previous">变化前的面板状态。</param>
        /// <param name="current">变化后的面板状态。</param>
        private void SynchronizeInitialPresentationAfterStateChange(UIState previous, UIState current)
        {
            // 状态变化可能结束过渡，再检查一次是否可以补同步。
            TrySynchronizeInitialPresentation();
        }

        /// <summary>仅在初始化完成且面板处于稳定状态时补同步一次动效。</summary>
        private void TrySynchronizeInitialPresentation()
        {
            // 只有已启动、仍待同步且引用存活的组件才参与初始同步。
            if (!_hasStarted || !_initialPresentationPending || _isDestroyed || !this || !uiBase ||
                !uiBase.IsUIStateInitialized) return; // 面板状态尚未初始化时继续等待。

            // 不回放 UI 生命周期，也不把新效果加入已经捕获的异步行为快照。
            // 过渡中新增的效果等待稳定状态；后续正常行为先到达时会撤销补同步。
            if (uiBase.State == UIState.Showed) ExecuteShow();
            // 隐藏稳定态对应动效隐藏端，不启动额外过渡。
            else if (uiBase.State == UIState.Hided) ExecuteHide();
        }

        /// <summary>结束初始状态等待，并解除不再需要的状态变化订阅。</summary>
        private void CompleteInitialPresentation()
        {
            // 已完成同步的组件不重复解除订阅。
            if (!_initialPresentationPending) return;
            // 先结束等待标记，防止后续同步回调重入。
            _initialPresentationPending = false;
            // 解除仅用于首次同步的状态变化监听。
            if (uiBase) uiBase.OnStateChanged -= SynchronizeInitialPresentationAfterStateChange;
        }

        /// <summary>过滤已销毁的旧行为快照并执行同步显示。</summary>
        private void ExecuteShow()
        {
            // 委托快照可能仍包含已在同轮较早回调中移除的动效。
            if (_isDestroyed || !this || _effectLifetimeToken.IsCancellationRequested) return;
            // 正式显示行为到来后不再等待初始状态补同步。
            CompleteInitialPresentation();
            // 交给派生类应用同步显示表现。
            OnUIShow();
        }

        /// <summary>过滤已销毁的旧行为快照并执行同步隐藏。</summary>
        private void ExecuteHide()
        {
            // 组件已移除或寿命已取消时忽略旧隐藏委托快照。
            if (_isDestroyed || !this || _effectLifetimeToken.IsCancellationRequested) return;
            // 正式隐藏行为取代尚未执行的初始同步。
            CompleteInitialPresentation();
            // 交给派生类应用同步隐藏表现。
            OnUIHide();
        }

        /// <summary>在组件寿命约束下执行异步显示行为。</summary>
        /// <param name="token">取消当前显隐或姿态过渡的令牌。</param>
        /// <returns>显示行为完成、取消或失败时结束的任务。</returns>
        private UniTask ExecuteShowAsync(CancellationToken token) => ExecuteEffectAsync(true, token);
        /// <summary>在组件寿命约束下执行异步隐藏行为。</summary>
        /// <param name="token">取消当前显隐或姿态过渡的令牌。</param>
        /// <returns>隐藏行为完成、取消或失败时结束的任务。</returns>
        private UniTask ExecuteHideAsync(CancellationToken token) => ExecuteEffectAsync(false, token);

        /// <summary>联合操作与组件寿命令牌执行显隐，并区分组件移除与外部取消。</summary>
        /// <param name="showing">为真时执行显示，为假时执行隐藏。</param>
        /// <param name="operationToken">调用方显隐操作的取消令牌。</param>
        /// <returns>当前显隐行为在操作与组件寿命约束下的任务。</returns>
        private async UniTask ExecuteEffectAsync(bool showing, CancellationToken operationToken)
        {
            // 外部操作已取消时立即保留取消语义，不启动动效。
            operationToken.ThrowIfCancellationRequested();
            // 被销毁或已取消寿命的组件不再执行新异步行为。
            if (_isDestroyed || !this || _effectLifetimeToken.IsCancellationRequested) return;

            // 本次真实过渡接管表现，不再等待初始状态同步。
            CompleteInitialPresentation();
            // 包装整个虚方法，让派生控制器的准备与收尾也受同一寿命约束。
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(operationToken, _effectLifetimeToken);
            // 仅将组件自身移除导致的取消视为该动效正常退出。
            try
            {
                // 使用联合令牌等待派生类的完整显示流程。
                if (showing) await OnUIShowAsync(linked.Token);
                // 隐藏方向同样覆盖派生类准备、动画与收尾。
                else await OnUIHideAsync(linked.Token);
            }
            catch (OperationCanceledException) when (
                _effectLifetimeToken.IsCancellationRequested && !operationToken.IsCancellationRequested)
            {
                // 单独移除动效只结束该行为，不补终点，也不取消仍存活的面板。
            }

            // 外部取消、操作替换和面板销毁始终保留 UI 操作的取消语义。
            operationToken.ThrowIfCancellationRequested();
        }

        /// <summary>
        ///  当UI面板隐藏时
        /// </summary>
        protected virtual void OnUIHide()
        {
            // 同步隐藏精确写入隐藏端进度。
            EffectAmount = 1;
        }

        /// <summary>
        ///  当UI面板显示时
        /// </summary>
        protected virtual void OnUIShow()
        {
            // 同步显示精确写入显示端进度。
            EffectAmount = 0;
        }

        /// <summary>
        ///  UI面板隐藏时的动效
        /// </summary>
        /// <param name="token">取消当前显隐或姿态过渡的令牌。</param>
        /// <returns>隐藏过渡完成、取消或失败时结束的任务。</returns>
        protected virtual async UniTask OnUIHideAsync(CancellationToken token)
        {
            // 从当前进度过渡到隐藏端，未配置曲线时使用线性时间。
            await UITransition.RunAsync(EffectAmount, 1, value => EffectAmount = value, hideDuration, token,
                hideEase == Ease.NotSet ? Ease.Linear : hideEase);
        }

        /// <summary>
        ///  UI面板显示时的动效
        /// </summary>
        /// <param name="token">取消当前显隐或姿态过渡的令牌。</param>
        /// <returns>显示过渡完成、取消或失败时结束的任务。</returns>
        protected virtual async UniTask OnUIShowAsync(CancellationToken token)
        {
            // 从当前进度过渡到显示端，允许取消中途状态继续参与下一次动画。
            await UITransition.RunAsync(EffectAmount, 0, value => EffectAmount = value, showDuration, token,
                showEase == Ease.NotSet ? Ease.Linear : showEase);
        }

        /// <summary>
        ///  销毁时解除监听；自身销毁令牌同时停止正在执行的动效。
        /// </summary>
        protected virtual void OnDestroy()
        {
            // 先标记销毁，尚未执行的旧行为快照将立即失效。
            _isDestroyed = true;
            // 面板仍存在时逐项解除本组件建立的生命周期订阅。
            if (uiBase != null)
            {
                // 移除异步显示行为，后续操作不再等待本动效。
                uiBase.ShowAsyncActions -= ExecuteShowAsync;
                // 移除异步隐藏行为，后续操作不再等待本动效。
                uiBase.HideAsyncActions -= ExecuteHideAsync;
                // 移除同步显示行为，避免委托继续持有已销毁组件。
                uiBase.ShowActions -= ExecuteShow;
                // 移除同步隐藏行为，避免委托继续持有已销毁组件。
                uiBase.HideActions -= ExecuteHide;
                // 即使尚未完成首次同步，也解除状态变化监听。
                uiBase.OnStateChanged -= SynchronizeInitialPresentationAfterStateChange;
            }
        }

        /// <summary>
        ///  当动效进度改变时
        /// </summary>
        /// <param name="value">动效进度,0==显示，1==隐藏</param>
        protected virtual void OnEffectAmountChanged(float value)
        {
        }

        /// <summary>在编辑器中补齐组件引用并应用当前预览进度。</summary>
        protected virtual void OnValidate()
        {
            // 重新取得当前节点上的面板供编辑器预览使用。
            uiBase = GetComponent<UIBase>();
            // 同步透明度容器引用，兼容编辑器组件增删。
            canvasGroup = GetComponent<CanvasGroup>();
            // 通过统一进度入口刷新派生动效的预览表现。
            EffectAmount = effectAmount;
        }
    }
}

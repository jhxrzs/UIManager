using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace JH
{
    /// <summary>
    ///  UI面板基类，实现UI的同步显隐、异步显隐以及层级关系联动。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public partial class UIBase : MonoBehaviour, IShowUI, IHideUI, IShowUIAsync, IHideUIAsync
    {

        /// <summary>
        ///  UI互斥组。
        /// </summary>
        [SerializeField, Tooltip("UI互斥组，同组UI无法同时显示")]
        private int mutexGroup;

        /// <summary>
        ///  同父节点且同互斥组UI发生显示竞争时的抢占优先级。
        /// </summary>
        [SerializeField, Min(0), Tooltip("同父节点且同互斥组的显示抢占优先级，数值越大越优先")]
        private int mutexPriority;

        /// <summary>
        ///  是否固定UI层级。
        /// </summary>
        [SerializeField, Tooltip("固定UI层级")]
        private bool fixedSiblingIndex;

        /// <summary>
        ///  直接父UI显示时是否自动显示当前UI。
        /// </summary>
        [SerializeField, Tooltip("直接父UI显示时自动显示当前UI")]
        private bool showWithParent;

        /// <summary>
        ///  外部直接显示当前UI时处理连续父UI链的策略。
        /// </summary>
        [SerializeField, Tooltip("外部直接显示当前UI时处理连续父UI链的策略")]
        private UIParentShowPolicy parentShowPolicy;

        /// <summary>
        ///  当前UI的状态。
        /// </summary>
        [SerializeField, Tooltip("当前UI状态")]
        private UIState state;

        /// <summary>
        ///  当前UI的序列化显隐状态是否已经由一次实际显隐流程接管初始化。
        /// </summary>
        private bool _isUIStateInitialized;

        /// <summary>供后挂载动效区分已执行的状态与尚待初始化的序列化初态。</summary>
        internal bool IsUIStateInitialized => _isUIStateInitialized;

        /// <summary>
        ///  严格Showed序列化初态是否正在等待启用父UI完成初始化。
        /// </summary>
        private bool _isStrictInitialShowDeferred;

        /// <summary>
        ///  严格初态延后协程版本，用于失活恢复时淘汰旧协程。
        /// </summary>
        private int _strictInitialShowDeferredVersion;

        /// <summary>
        ///  当前异步显隐操作的取消源。
        /// </summary>
        private CancellationTokenSource _currentShowHideCts;

        /// <summary>
        ///  当前显隐操作的递增版本，用于阻止重入后的旧操作继续提交状态。
        /// </summary>
        private int _showHideOperationVersion;

        /// <summary>
        ///  最近一次成功登记的显示请求模式。
        /// </summary>
        private UIShowMode _currentShowMode;

        /// <summary>
        ///  最近一次显示请求模式对应的显隐操作版本。
        /// </summary>
        private int _showModeOperationVersion = -1;

        /// <summary>
        ///  当前父级联显示请求的直接发起父UI。
        /// </summary>
        private UIBase _currentParentCascadeSource;

        /// <summary>
        ///  当前父级联来源父UI发起显示事务时的显隐操作版本。
        /// </summary>
        private int _currentParentCascadeSourceOperationVersion = -1;

        /// <summary>
        ///  当前仍在执行显示生命周期的显隐操作版本。
        /// </summary>
        private int _activeShowOperationVersion = -1;

        /// <summary>
        ///  当前仍在执行隐藏生命周期的显隐操作版本。
        /// </summary>
        private int _activeHideOperationVersion = -1;

        /// <summary>
        ///  当前的同步隐藏行为委托。
        /// </summary>
        private Action _onUIHideEvent;

        /// <summary>
        ///  当前的同步显示行为委托。
        /// </summary>
        private Action _onUIShowEvent;

        /// <summary>
        ///  当前的异步显示行为委托。
        /// </summary>
        private Func<CancellationToken, UniTask> _onUIShowAsyncEvent;

        /// <summary>
        ///  当前的异步隐藏行为委托。
        /// </summary>
        private Func<CancellationToken, UniTask> _onUIHideAsyncEvent;

        /// <summary>
        ///  当UI状态改变时触发，参数依次为原始状态和新状态。
        /// </summary>
        public event Action<UIState, UIState> OnStateChanged;

        /// <summary>
        ///  UI显示前触发。
        /// </summary>
        public event Action BeforeUIShowEvent;

        /// <summary>
        ///  UI显示后触发。
        /// </summary>
        public event Action AfterUIShowedEvent;

        /// <summary>
        ///  UI隐藏前触发。
        /// </summary>
        public event Action BeforeUIHideEvent;

        /// <summary>
        ///  UI隐藏后触发。
        /// </summary>
        public event Action AfterUIHidedEvent;

        /// <summary>
        ///  增加UI同步显示行为的事件。
        /// </summary>
        public event Action ShowActions
        {
            add => _onUIShowEvent += value;
            remove => _onUIShowEvent -= value;
        }

        /// <summary>
        ///  增加UI同步隐藏行为的事件。
        /// </summary>
        public event Action HideActions
        {
            add => _onUIHideEvent += value;
            remove => _onUIHideEvent -= value;
        }

        /// <summary>
        ///  增加UI异步隐藏行为的事件。
        /// </summary>
        public event Func<CancellationToken, UniTask> HideAsyncActions
        {
            add => _onUIHideAsyncEvent += value;
            remove => _onUIHideAsyncEvent -= value;
        }

        /// <summary>
        ///  增加UI异步显示行为的事件。
        /// </summary>
        public event Func<CancellationToken, UniTask> ShowAsyncActions
        {
            add => _onUIShowAsyncEvent += value;
            remove => _onUIShowAsyncEvent -= value;
        }

        /// <summary>
        ///  直接父UI显示时是否自动显示当前UI。
        /// </summary>
        public bool ShowWithParent
        {
            get => showWithParent;
            set => showWithParent = value;
        }

        /// <summary>
        ///  外部直接显示当前UI时处理连续父UI链的策略。
        /// </summary>
        public UIParentShowPolicy ParentShowPolicy
        {
            get => parentShowPolicy;
            set => parentShowPolicy = value is UIParentShowPolicy.RequireParentChainShowedForDirectShow
                ? value
                : UIParentShowPolicy.AutoShowParentChain;
        }

        /// <summary>
        ///  同父节点且同互斥组UI发生显示竞争时的抢占优先级。
        /// </summary>
        public int MutexPriority
        {
            get => mutexPriority;
            set => mutexPriority = Mathf.Max(0, value);
        }

        /// <summary>
        ///  当前UI状态。
        /// </summary>
        public UIState State
        {
            get => state;
            private set
            {
                // 相同状态不重复派发生命周期事件。
                if (value == state) return;

                // 先提交新状态，确保事件订阅者读取State时得到新值。
                var previousState = state;
                // 保存新的生命周期状态。
                state = value;
                // 让射线阻挡同步到新的生命周期阶段。
                ApplyRaycastState(value);
                // 按订阅快照通知业务状态已经改变。
                UINotification.Invoke(this, nameof(OnStateChanged), OnStateChanged, previousState, value);
            }
        }

        /// <summary>保存面板显示时的射线配置，并使隐藏初态不再阻挡射线。</summary>
        protected virtual void Awake()
        {
            // 初始化当前状态对应的射线阻挡表现。
            ApplyRaycastState(State);
        }

        /// <summary>
        ///  Unity对象重新启用时恢复可能因GameObject失活而停止的严格初态延后协程。
        /// </summary>
        protected virtual void OnEnable()
        {
            // 已被公开API或父级联接管的UI不再需要恢复序列化初态协程。
            if (_isUIStateInitialized)
            {
                // 已初始化的面板不再保留严格初态延后标记。
                _isStrictInitialShowDeferred = false;
                // 结束重新启用时的初态检查。
                return;
            }

            // 首次OnEnable发生在Start之前，只有已经进入延后状态的UI才需要续接。
            if (_isStrictInitialShowDeferred) DeferStrictInitialShow();
        }

        /// <summary>
        ///  Unity首次启用组件时初始化序列化状态。
        /// </summary>
        protected virtual void Start()
        {
            // Start前已经由公开API或父UI执行显隐流程时，不再重复派发初始化生命周期。
            if (_isUIStateInitialized) return;

            // 启用中的父UI尚未执行Start时，延后严格Showed初态以消除父子Start顺序差异。
            if (ShouldDeferStrictInitialShow())
            {
                // 将首次显示推迟到连续父链完成初始化之后。
                DeferStrictInitialShow();
                // 由延后流程负责初始化，当前 Start 不再继续。
                return;
            }

            // 初始化也经过同一调度边界，回调提交的公开请求不能插入初始化过程。
            UIOperationDispatcher.Run(InitializeUIState);
        }

        /// <summary>
        ///  提交同步显示请求；空闲时立即执行，调度回调中的请求按顺序等待。
        /// </summary>
        public void Show()
        {
            // 将公开显示请求连同目标和方向提交到统一队列。
            UIOperationDispatcher.Run(this, true, ExecuteShow);
        }

        /// <summary>执行普通显示；框架内部父链联动直接调用此入口。</summary>
        private void ExecuteShow()
        {
            // 普通入口不放宽严格祖先必须完成显示的条件。
            ExecuteShow(allowScheduledParents: false);
        }

        /// <summary>内部补父链可复用已完成关系调度的祖先，外部请求仍要求严格父链已显示。</summary>
        /// <param name="allowScheduledParents">是否允许复用已完成关系调度但动画尚未结束的祖先。</param>
        private void ExecuteShow(bool allowScheduledParents)
        {
            // 已销毁目标不再接受显示生命周期。
            if (!this) return;
            // 已经显示时无需重复执行生命周期。
            if (_isUIStateInitialized && State is UIState.Showed) return;

            // 父链未就绪或更高优先级同组兄弟活跃时，本次请求保持零副作用拒绝。
            if (!CanPreflightShow(allowScheduledParents)) return;

            // 通过准入后才计入本次真实显示行为。
            UIOperationDispatcher.BeginBehavior(this, true);

            // 通过统一普通显示入口执行完整同步生命周期。
            ShowNotCheckState(UIShowMode.Normal, allowScheduledParents: allowScheduledParents);
        }

        /// <summary>
        ///  提交同步隐藏请求；空闲时立即执行，调度回调中的请求按顺序等待。
        /// </summary>
        public void Hide()
        {
            // 将公开隐藏请求连同目标和方向提交到统一队列。
            UIOperationDispatcher.Run(this, false, ExecuteHide);
        }

        /// <summary>交互切换作为一条请求入队，方向按出队时的状态确定。</summary>
        internal void Switch()
        {
            // 将方向尚未确定的切换作为一条请求排队。
            UIOperationDispatcher.Run(this, null, () =>
            {
                // 请求出队时再次跳过已销毁目标。
                if (!this) return;
                // 直接进入本次生命周期，避免再次入队打乱后续请求的顺序。
                if (State is UIState.Showed or UIState.Showing) ExecuteHide();
                // 当前状态尚未显示时切换为显示。
                else ExecuteShow();
            });
        }

        /// <summary>异步交互切换；返回任务跟踪实际选中的完整显隐生命周期。</summary>
        /// <param name="cancellationToken">调用方用于取消本次异步操作的令牌。</param>
        /// <returns>跟踪本次实际选定显隐方向的完整异步任务。</returns>
        internal UniTask SwitchAsync(CancellationToken cancellationToken)
        {
            // 按实际出队状态选择显隐方向，并返回该请求的异步结果。
            return UIOperationDispatcher.RunAsync(this, null, () =>
                State is UIState.Showed or UIState.Showing
                    ? ExecuteHideAsync(cancellationToken)
                    : ExecuteShowAsync(cancellationToken), cancellationToken);
        }

        /// <summary>执行隐藏；框架内部子树和互斥联动直接调用此入口。</summary>
        private void ExecuteHide()
        {
            // 独立隐藏入口创建自己的子树资格上下文。
            ExecuteHide(null);
        }

        /// <summary>递归隐藏沿用本轮子树入口资格，避免前序回调失活祖先后漏掉后代。</summary>
        /// <param name="hideContext">本轮递归隐藏共享的父子关系资格上下文。</param>
        private void ExecuteHide(UIHideContext hideContext)
        {
            // 已销毁目标不再执行隐藏生命周期。
            if (!this) return;
            // 已隐藏且没有处于入口重入窗口的显示预约时无需执行生命周期。
            if (State is UIState.Hided)
            {
                // Hided状态下的有效显示预约只需失效版本并取消令牌，不派发虚假隐藏事件。
                if (IsCurrentShowAction()) CancelHidedShowReservation();
                // 已隐藏节点在预约处理后保持状态短路。
                return;
            }

            // 开始副作用前检查并登记本次实际隐藏行为。
            UIOperationDispatcher.BeginBehavior(this, false);

            // 其余可见生命周期通过统一同步隐藏入口完整收口。
            HideNotCheckState(hideContext);
        }

        /// <summary>
        ///  提交异步显示请求，返回跟踪完整生命周期的任务；等待行为期间不占用调度器。
        /// </summary>
        /// <param name="cancellationToken">取消当前显示操作的令牌。</param>
        /// <returns>表示完整显示生命周期的异步任务。</returns>
        /// <exception cref="OperationCanceledException">操作取消或被更新请求取代时抛出。</exception>
        public UniTask ShowAsync(CancellationToken cancellationToken)
        {
            // 将异步显示提交到队列，并把调用方取消关联到该请求。
            return UIOperationDispatcher.RunAsync(this, true, () => ExecuteShowAsync(cancellationToken), cancellationToken);
        }

        /// <summary>在状态短路和只读预演通过后启动普通异步显示生命周期。</summary>
        /// <param name="cancellationToken">调用方用于取消本次异步操作的令牌。</param>
        /// <param name="allowScheduledParents">是否允许复用已完成关系调度但动画尚未结束的祖先。</param>
        /// <returns>跟踪本轮显示完成、准入拒绝、取消或失败的任务。</returns>
        private async UniTask ExecuteShowAsync(CancellationToken cancellationToken, bool allowScheduledParents = false)
        {
            // 预取消必须在任何状态和事件副作用发生前被观察。
            ThrowIfShowHideCancellationRequested(cancellationToken);
            // 已初始化且完成显示时保留重复显示的空操作语义。
            if (_isUIStateInitialized && State is UIState.Showed) return;

            // 正常仲裁拒绝不属于异常，且不得取消当前操作或改变任何UI状态。
            if (!CanPreflightShow(allowScheduledParents)) return;

            // 取消异常属于公开契约的一部分，不在此处吞掉。
            UIOperationDispatcher.BeginBehavior(this, true);
            // 等待当前普通显示流程，恢复时仍沿用原请求来源。
            await UIOperationDispatcher.Continue(ShowNotCheckStateAsync(cancellationToken, UIShowMode.Normal, allowScheduledParents: allowScheduledParents));
        }

        /// <summary>
        ///  由直接父UI同步显示当前UI，并阻止当前UI再次向上显示父链。
        /// </summary>
        /// <param name="cascadeParent">发起本次直接子UI级联显示的父UI。</param>
        /// <param name="cascadeParentOperationVersion">来源父UI本轮显示事务的显隐操作版本。</param>
        private void ShowFromParent(UIBase cascadeParent, int cascadeParentOperationVersion)
        {
            // 已初始化且已显示的子UI无需重复执行；未初始化初态仍需补齐实际显示效果。
            if (_isUIStateInitialized && State is UIState.Showed) return;

            // 显式Showing请求必须继续等待当前父UI，不能由默认级联反向取代。
            if (State is UIState.Showing && IsCurrentNormalShowAction()) return;

            // 父级联绕过父链准入，但仍服从同组兄弟的数值显示优先级。
            if (!CanScheduleShow(
                    UIShowMode.ParentCascade,
                    cascadeParent,
                    // 未通过父级联准入时不启动显示行为。
                    cascadeParentOperationVersion)) return;

            // 父级联入口只省略向上显示父链，当前UI自身生命周期保持完整。
            ShowNotCheckState(
                UIShowMode.ParentCascade,
                cascadeParent,
                cascadeParentOperationVersion);
        }

        /// <summary>
        ///  由直接父UI异步显示当前UI，并阻止当前UI再次向上显示父链。
        /// </summary>
        /// <param name="cancellationToken">直接父UI本轮显示操作的取消令牌。</param>
        /// <param name="cascadeParent">发起本次直接子UI级联显示的父UI。</param>
        /// <param name="cascadeParentOperationVersion">来源父UI本轮显示事务的显隐操作版本。</param>
        /// <returns>当前UI完整显示生命周期对应的异步任务。</returns>
        private async UniTask ShowFromParentAsync(CancellationToken cancellationToken, UIBase cascadeParent,
            int cascadeParentOperationVersion)
        {
            // 父操作预取消时不得改变子UI状态或Start初始化标记。
            ThrowIfShowHideCancellationRequested(cancellationToken);

            // 已初始化且已显示的子UI无需重复执行；未初始化初态仍需补齐实际显示效果。
            if (_isUIStateInitialized && State is UIState.Showed) return;

            // 显式Showing请求必须继续等待当前父UI，不能由默认级联反向取代。
            if (State is UIState.Showing && IsCurrentNormalShowAction()) return;

            // 父级联绕过父链准入，但仍服从同组兄弟的数值显示优先级。
            if (!CanScheduleShow(
                    UIShowMode.ParentCascade,
                    cascadeParent,
                    // 异步父级联未获准时直接结束当前请求。
                    cascadeParentOperationVersion)) return;

            // 子UI使用链接令牌完成自身生命周期，但不再递归等待父UI。
            await UIOperationDispatcher.Continue(ShowNotCheckStateAsync(
                cancellationToken,
                UIShowMode.ParentCascade,
                cascadeParent,
                cascadeParentOperationVersion));
        }

        /// <summary>
        ///  提交异步隐藏请求，返回跟踪完整生命周期的任务；等待行为期间不占用调度器。
        /// </summary>
        /// <param name="cancellationToken">取消当前隐藏操作的令牌。</param>
        /// <returns>表示完整隐藏生命周期的异步任务。</returns>
        /// <exception cref="OperationCanceledException">操作取消或被更新请求取代时抛出。</exception>
        public UniTask HideAsync(CancellationToken cancellationToken)
        {
            // 将异步隐藏提交到队列，并返回完整生命周期任务。
            return UIOperationDispatcher.RunAsync(this, false, () => ExecuteHideAsync(cancellationToken), cancellationToken);
        }

        /// <summary>观察入口取消并保留隐藏状态短路，然后执行异步隐藏生命周期。</summary>
        /// <param name="cancellationToken">调用方用于取消本次异步操作的令牌。</param>
        /// <returns>跟踪本轮隐藏完成、状态短路、取消或失败的任务。</returns>
        private async UniTask ExecuteHideAsync(CancellationToken cancellationToken)
        {
            // 预取消必须在任何状态和事件副作用发生前被观察。
            ThrowIfShowHideCancellationRequested(cancellationToken);
            // 已隐藏状态只处理仍有效的显示预约。
            if (State is UIState.Hided)
            {
                // 异步显示尚处于Hided预约窗口时只取消预约，原显示任务会传播更新取消。
                if (IsCurrentShowAction()) CancelHidedShowReservation();
                // 完成预约取消后无需再启动隐藏生命周期。
                return;
            }

            // 取消异常属于公开契约的一部分，不在此处吞掉。
            UIOperationDispatcher.BeginBehavior(this, false);
            // 等待异步隐藏，并把后续生命周期恢复到调度队列。
            await UIOperationDispatcher.Continue(HideNotCheckStateAsync(cancellationToken));
        }

        /// <summary>
        ///  执行全部同步显示行为。
        /// </summary>
        protected virtual void OnUIShow()
        {
            // 委托为空时安全跳过。
            _onUIShowEvent?.Invoke();
        }

        /// <summary>
        ///  执行全部同步隐藏行为。
        /// </summary>
        protected virtual void OnUIHide()
        {
            // 委托为空时安全跳过。
            _onUIHideEvent?.Invoke();
        }

        /// <summary>
        ///  并行执行全部异步显示行为。
        /// </summary>
        /// <param name="cancellationToken">传递给每个显示行为的操作令牌。</param>
        /// <returns>全部显示行为结束后的任务。</returns>
        protected virtual async UniTask OnUIShowAsync(CancellationToken cancellationToken)
        {
            // 显示前事件重入显隐时，不得再执行旧操作的同步或异步处理器。
            cancellationToken.ThrowIfCancellationRequested();

            // 没有异步显示委托时执行同步钩子，保持ShowAsync与Show的基础行为一致。
            if (_onUIShowAsyncEvent == null)
            {
                // 没有异步显示处理器时执行同步显示表现。
                OnUIShow();
                // 同步回退已结束，不再启动异步处理器集合。
                return;
            }

            // 使用共享列表池保存本轮任务，避免每个UI长期持有任务集合。
            var taskList = ListPool<UniTask>.Get();
            // 保证处理器启动或等待失败时都归还已启动任务列表。
            try
            {
                // GetInvocationList提供稳定的本轮委托快照。
                var invocationList = _onUIShowAsyncEvent.GetInvocationList();
                // 单独保存处理器同步调用阶段产生的异常。
                Exception invocationException = null;
                // 按注册快照顺序启动显示处理器。
                for (var i = 0; i < invocationList.Length; i++)
                {
                    // 取消后不再启动新行为，已启动行为仍由WhenAll完整观察。
                    if (cancellationToken.IsCancellationRequested) break;
                    // 将当前订阅者还原为异步显示委托。
                    var showAction = (Func<CancellationToken, UniTask>)invocationList[i];
                    // 隔离处理器启动时抛出的异常，仍观察此前已启动任务。
                    try
                    {
                        // 普通委托可能在返回UniTask之前同步抛错。
                        taskList.Add(showAction.Invoke(cancellationToken));
                    }
                    catch (Exception exception)
                    {
                        // 停止启动后续行为，但仍需观察此前已经启动的任务。
                        invocationException = exception;
                        // 当前启动失败后不再启动后续处理器。
                        break;
                    }
                }

                // 保存组合任务异常，确保它能与同步委托异常一起报告。
                Exception taskException = null;
                // 等待全部已启动显示行为，并单独记录等待阶段的异常。
                try
                {
                    // 组合器不使用生命周期令牌提前结束等待。
                    await UIOperationDispatcher.Continue(UIAsync.WaitAll(taskList));
                }
                catch (Exception exception)
                {
                    // 保存已启动显示任务的失败结果。
                    taskException = exception;
                }

                // 已启动任务全部结束后再传播一个或多个失败原因。
                ThrowHandlerExceptions(invocationException, taskException);
                // 行为全部收尾后再次观察调用方取消。
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                // 成功、取消或异常时都归还临时任务列表。
                ListPool<UniTask>.Release(taskList);
            }
        }

        /// <summary>
        ///  并行执行全部异步隐藏行为。
        /// </summary>
        /// <param name="cancellationToken">传递给每个隐藏行为的操作令牌。</param>
        /// <returns>全部隐藏行为结束后的任务。</returns>
        protected virtual async UniTask OnUIHideAsync(CancellationToken cancellationToken)
        {
            // 隐藏前事件重入显隐时，不得再执行旧操作的同步或异步处理器。
            cancellationToken.ThrowIfCancellationRequested();

            // 没有异步隐藏委托时执行同步钩子，保持HideAsync与Hide的基础行为一致。
            if (_onUIHideAsyncEvent == null)
            {
                // 没有异步隐藏处理器时执行同步隐藏表现。
                OnUIHide();
                // 同步回退结束后不再启动异步处理器集合。
                return;
            }

            // 使用共享列表池保存本轮任务。
            var taskList = ListPool<UniTask>.Get();
            // 保证隐藏处理器启动或等待失败时都归还任务列表。
            try
            {
                // GetInvocationList提供稳定的本轮委托快照。
                var invocationList = _onUIHideAsyncEvent.GetInvocationList();
                // 单独记录隐藏处理器同步启动阶段的异常。
                Exception invocationException = null;
                // 按注册快照顺序启动隐藏处理器。
                for (var i = 0; i < invocationList.Length; i++)
                {
                    // 取消后不再启动新行为，已启动行为仍由WhenAll完整观察。
                    if (cancellationToken.IsCancellationRequested) break;
                    // 将当前订阅者还原为异步隐藏委托。
                    var hideAction = (Func<CancellationToken, UniTask>)invocationList[i];
                    // 隔离处理器启动异常，保留已启动行为的收尾机会。
                    try
                    {
                        // 普通委托可能在返回UniTask之前同步抛错。
                        taskList.Add(hideAction.Invoke(cancellationToken));
                    }
                    catch (Exception exception)
                    {
                        // 停止启动后续行为，但仍需观察此前已经启动的任务。
                        invocationException = exception;
                        // 隐藏处理器启动失败后停止启动后续处理器。
                        break;
                    }
                }

                // 保存组合任务异常，确保它能与同步委托异常一起报告。
                Exception taskException = null;
                // 观察全部已启动隐藏行为，并捕获等待阶段的异常。
                try
                {
                    // 子任务自行传播异常，最终检查处理忽略令牌的行为。
                    await UIOperationDispatcher.Continue(UIAsync.WaitAll(taskList));
                }
                catch (Exception exception)
                {
                    // 保存已启动隐藏任务的失败结果。
                    taskException = exception;
                }

                // 已启动任务全部结束后再传播一个或多个失败原因。
                ThrowHandlerExceptions(invocationException, taskException);
                // 所有隐藏行为结束后再次观察调用方取消。
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                // 成功、取消或异常时都归还临时任务列表。
                ListPool<UniTask>.Release(taskList);
            }
        }

        /// <summary>
        ///  执行不带状态短路的同步显示流程。
        /// </summary>
        /// <param name="showMode">当前显示请求的来源模式。</param>
        /// <param name="parentCascadeSource">父级联请求的直接发起父UI；普通显示传null。</param>
        /// <param name="parentCascadeSourceOperationVersion">来源父UI显示事务版本；普通显示传-1。</param>
        /// <param name="allowScheduledParents">内部补父链时允许祖先仍在已完成关系调度的显示过渡中。</param>
        private void ShowNotCheckState(UIShowMode showMode, UIBase parentCascadeSource = null,
            int parentCascadeSourceOperationVersion = -1, bool allowScheduledParents = false)
        {
            // 新版本必须在取消旧异步操作前登记显示预约，使取消回调能够参与兄弟优先级仲裁。
            var operationVersion = BeginSynchronousShowHideAction(
                showMode,
                parentCascadeSource,
                parentCascadeSourceOperationVersion);

            // 仅为本轮允许的内部补父操作登记祖先状态放宽版本。
            if (allowScheduledParents) _allowScheduledParentsVersion = operationVersion;
            // 借用兄弟列表，保存本轮互斥协调所需的关系。
            var siblings = ListPool<UIBase>.Get();
            // 借用父链列表，保存需要补齐的连续祖先。
            var parents = ListPool<UIBase>.Get();
            // 借用子界面列表，保存本轮随父显示候选。
            var children = ListPool<UIBase>.Get();
            // 无论显示成功、失败或提前退出，都释放本轮关系快照。
            try
            {
                // 取消回调重入后，旧流程不再继续。
                if (!IsCurrentShowHideAction(operationVersion)) return;

                // 取消回调可能改变父链或兄弟关系，已登记操作失去准入时必须收口旧过渡状态。
                if (!MaintainCurrentShowAdmission(showMode, operationVersion)) return;

                // 通过二次准入后才接管序列化初态，被拒绝的预约不影响Start。
                _isUIStateInitialized = true;

                // 完成二次准入后进入同步显示前关系联动。
                if (!BeforeUIShowInvoke(siblings, parents, operationVersion, showMode)) return;

                // 执行当前面板自身的同步显示行为。
                OnUIShow();
                // 自身显示行为产生更新请求后停止旧流程。
                if (!IsCurrentShowHideAction(operationVersion)) return;

                // 显示行为可能动态换父或激活新兄弟，处理子树前按最新层级重新协调互斥组。
                if (!ReconcileCurrentShowSiblings(siblings, showMode, operationVersion)) return;

                // 当前面板完成显示行为后，再按层级顺序显示配置为随父显示的直接子UI。
                if (!ShowChildrenWithParentInvoke(children, operationVersion)) return;

                // 子UI生命周期同样可以修改父级兄弟关系，提交Showed前必须再次达到稳定互斥状态。
                if (!ReconcileCurrentShowSiblings(siblings, showMode, operationVersion)) return;

                // 使用同一份兄弟快照完成显示后流程。
                AfterUIShowedInvoke(siblings, operationVersion, showMode);

                // StateChanged、层级调整和After回调都可能再次改变关系，返回前完成最终动态协调。
                ReconcileCurrentShowSiblings(siblings, showMode, operationVersion);
            }
            finally
            {
                // 分层清理显示活动登记与各个池化关系列表。
                try
                {
                    // 临时关系列表不挂在UI实例上，避免常驻内存膨胀。
                    try
                    {
                        // 归还本轮随父显示子界面列表。
                        ListPool<UIBase>.Release(children);
                    }
                    finally
                    {
                        // 保证父链列表释放异常时仍尝试归还兄弟列表。
                        try
                        {
                            // 归还本轮连续祖先列表。
                            ListPool<UIBase>.Release(parents);
                        }
                        finally
                        {
                            // 归还本轮互斥兄弟列表。
                            ListPool<UIBase>.Release(siblings);
                        }
                    }
                }
                finally
                {
                    // 成功、异常或被重入取代时都结束本轮显示活动登记。
                    CompleteCurrentShowAction(operationVersion);
                }
            }
        }

        /// <summary>
        ///  执行不带状态短路的同步隐藏流程。
        /// </summary>
        /// <param name="hideContext">本轮递归隐藏共享的父子关系资格上下文。</param>
        private void HideNotCheckState(UIHideContext hideContext = null)
        {
            // 在取消旧操作的回调前保留子树资格；递归调用共用上下文，由最外层释放。
            using var ownedHideContext = hideContext == null ? UIHideContext.Capture(this) : null;
            // 递归隐藏沿用来源上下文，独立隐藏使用刚创建的上下文。
            hideContext ??= ownedHideContext;
            // 后续分支可能刚加入活动子节点；在取消该分支旧操作前补齐这些新关系。
            if (ownedHideContext == null) hideContext.CaptureChildren(this);

            // 先登记新版本再取消旧异步操作。
            var operationVersion = BeginSynchronousShowHideAction();

            // 隐藏活动版本用于区分真正执行中的Hiding与取消或故障残留状态。
            RegisterCurrentHideAction(operationVersion);

            // 成功登记的实际隐藏流程接管序列化初态，Start不得再次重复初始化。
            _isUIStateInitialized = true;
            // 借用列表保存本层直接子界面的隐藏快照。
            var children = ListPool<UIBase>.Get();
            // 保证隐藏提前结束或抛错时仍清理快照和活动登记。
            try
            {
                // 取消回调重入后，旧流程不再继续。
                if (!IsCurrentShowHideAction(operationVersion)) return;

                // 在生命周期开始时捕获直接子UI快照。
                FindChildren(children, hideContext);
                // 子树或隐藏前通知使本轮失效时停止旧流程。
                if (!BeforeUIHideInvoke(children, operationVersion, hideContext)) return;

                // 执行当前面板自身的同步隐藏行为。
                OnUIHide();
                // 自身隐藏行为完成后再次确认当前操作未被取代。
                if (!IsCurrentShowHideAction(operationVersion)) return;
                // 提交隐藏完成状态并派发完成通知。
                AfterUIHidedInvoke(operationVersion);
            }
            finally
            {
                // 即使快照归还失败也要结束本轮隐藏活动登记。
                try
                {
                    // 归还本轮直接子UI快照。
                    ListPool<UIBase>.Release(children);
                }
                finally
                {
                    // 同步隐藏完成、异常或被重入取代时都结束本轮隐藏活动登记。
                    CompleteCurrentHideAction(operationVersion);
                }
            }
        }

        /// <summary>
        ///  执行不带状态短路的异步显示流程。
        /// </summary>
        /// <param name="cancellationToken">调用方提供的取消令牌。</param>
        /// <param name="showMode">当前显示请求的来源模式。</param>
        /// <param name="parentCascadeSource">父级联请求的直接发起父UI；普通显示传null。</param>
        /// <param name="parentCascadeSourceOperationVersion">来源父UI显示事务版本；普通显示传-1。</param>
        /// <returns>本轮显示流程任务。</returns>
        /// <param name="allowScheduledParents">是否允许复用已完成关系调度但动画尚未结束的祖先。</param>
        private async UniTask ShowNotCheckStateAsync(CancellationToken cancellationToken, UIShowMode showMode,
            UIBase parentCascadeSource = null, int parentCascadeSourceOperationVersion = -1, bool allowScheduledParents = false)
        {
            // 进入本轮操作前观察预取消和对象销毁。
            ThrowIfShowHideCancellationRequested(cancellationToken);

            // 新版本必须在取消旧操作前登记显示预约，使重入兄弟能够观察本轮优先级。
            var operationCts = BeginAsynchronousShowHideAction(
                cancellationToken,
                out var operationVersion,
                showMode,
                parentCascadeSource,
                parentCascadeSourceOperationVersion);

            // 为内部异步补父链记录允许复用已调度祖先的操作版本。
            if (allowScheduledParents) _allowScheduledParentsVersion = operationVersion;
            // 借用兄弟列表供异步显示关系协调使用。
            var siblings = ListPool<UIBase>.Get();
            // 借用列表保存本轮必须补齐的父链。
            var parents = ListPool<UIBase>.Get();
            // 借用列表保存本轮随父显示的子界面。
            var children = ListPool<UIBase>.Get();
            // 借用列表统一观察本轮已启动的表现和关联任务。
            var tasks = ListPool<UniTask>.Get();
            // 为异步显示的异常传播与全部资源清理建立收尾边界。
            try
            {
                // 保存同步关系调度阶段的失败，避免遗漏已启动任务。
                Exception schedulingException = null;
                // 即使关系调度中断，也先记录错误并继续观察已启动表现。
                try
                {
                    // 生命周期启动前确认目标仍持有显示准入。
                    MaintainCurrentShowAdmission(showMode, operationVersion, operationCts);
                    // 本轮真实显示接管序列化初态，防止 Start 重复初始化。
                    _isUIStateInitialized = true;

                    // 这些方法先同步完成所有关系调度，返回的任务只等待已启动的表现。
                    tasks.Add(BeforeUIShowAsyncInvoke(siblings, parents, operationVersion, operationCts, showMode));
                    // 启动自身异步表现前再次检查操作令牌。
                    operationCts.Token.ThrowIfCancellationRequested();
                    // 将自身显示表现纳入本轮必须观察的任务集合。
                    tasks.Add(OnUIShowAsync(operationCts.Token));
                    // 协调自身表现启动期间可能变化的互斥兄弟。
                    tasks.Add(ReconcileCurrentShowSiblingsAsync(siblings, showMode, operationVersion, operationCts));
                    // 启动随父显示子界面，并跟踪这些关联任务。
                    tasks.Add(ShowChildrenWithParentAsyncInvoke(children, operationVersion, operationCts));
                    // 子界面回调完成后再次协调可能变化的互斥关系。
                    tasks.Add(ReconcileCurrentShowSiblingsAsync(siblings, showMode, operationVersion, operationCts));
                    // 完成关系调度前确认显示准入仍有效。
                    MaintainCurrentShowAdmission(showMode, operationVersion, operationCts);
                    // 登记本轮显示关系已就绪，允许后续内部父链复用。
                    _showRelationsVersion = operationVersion;
                    // 在当前同步调度阶段完成面板显示置顶。
                    BringToFront(siblings);
                }
                catch (Exception exception)
                {
                    // 覆写钩子即使在返回任务前抛错，已启动的其他表现也必须被观察。
                    schedulingException = exception;
                }

                // 单独保存所有已启动表现收尾后的失败。
                Exception presentationException = null;
                // 等待表现收尾，不让同步调度异常造成后台任务遗留。
                try
                {
                    // 观察全部已启动任务，并在原请求上下文中恢复。
                    await UIOperationDispatcher.Continue(UIAsync.WaitAll(tasks));
                }
                catch (Exception exception)
                {
                    // 保存异步表现阶段产生的异常。
                    presentationException = exception;
                }
                // 合并或按原始堆栈传播调度异常与表现异常。
                ThrowHandlerExceptions(schedulingException, presentationException);

                // 异步恢复只复核并提交完成状态，不再打开父链、隐藏兄弟或发现默认子节点。
                MaintainCommittedShowRelations(siblings, showMode, operationVersion, operationCts);
                // 关联任务完成后提交显示成功，避免重复调整兄弟顺序。
                AfterUIShowedInvoke(siblings, operationVersion, showMode, operationCts, updateSiblingIndex: false);
                // 完成通知后仅核验已提交关系，不重新启动关系调度。
                MaintainCommittedShowRelations(siblings, showMode, operationVersion, operationCts);
                // 成功返回前再次检查版本和操作取消。
                ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);
            }
            catch (Exception operationException)
            {
                // 失败时取消本轮派生操作，同时保留原始异常和取消回调异常。
                CancelFailedShowHideAction(operationCts, operationException);
                // 原样传播本轮显示失败，保留调用方可观察的结果。
                throw;
            }
            finally
            {
                // 分层保证活动登记、关系快照和链接取消源都能清理。
                try
                {
                    // 归还本轮直接子UI快照。
                    ListPool<UIBase>.Release(children);
                }
                finally
                {
                    // 释放子界面列表后继续清理父链和兄弟快照。
                    try
                    {
                        // 即使第一个列表归还失败，也必须释放祖先快照。
                        ListPool<UIBase>.Release(parents);
                    }
                    finally
                    {
                        // 释放父链列表后继续清理兄弟快照及任务列表。
                        try
                        {
                            // 兄弟快照始终在局部取消源释放前归还。
                            ListPool<UIBase>.Release(siblings);
                        }
                        finally
                        {
                            // 即使兄弟列表释放异常也继续释放任务和取消源。
                            try
                            {
                                // 用户处理器失败或取消时同样结束本轮显示活动登记。
                                CompleteCurrentShowAction(operationVersion);
                            }
                            finally
                            {
                                // 归还已完成观察的关联任务列表。
                                ListPool<UniTask>.Release(tasks);
                                // 解除本轮链接取消注册并避免清理较新的操作引用。
                                CompleteAsynchronousShowHideAction(operationCts);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        ///  执行不带状态短路的异步隐藏流程。
        /// </summary>
        /// <param name="cancellationToken">调用方提供的取消令牌。</param>
        /// <returns>本轮隐藏流程任务。</returns>
        private async UniTask HideNotCheckStateAsync(CancellationToken cancellationToken)
        {
            // 进入本轮操作前观察预取消和对象销毁。
            ThrowIfShowHideCancellationRequested(cancellationToken);

            // 取消旧操作也可能失活祖先，必须在任何业务回调前保留本轮资格。
            using var hideContext = UIHideContext.Capture(this);

            // 在隐藏前事件发生前登记本轮操作，修复事件重入覆盖。
            var operationCts = BeginAsynchronousShowHideAction(cancellationToken, out var operationVersion);

            // 登记当前异步隐藏活动，使Hiding仅在有效操作生存期内持有互斥优先级。
            RegisterCurrentHideAction(operationVersion);

            // 成功登记的异步隐藏流程接管序列化初态初始化。
            _isUIStateInitialized = true;
            // 借用列表保存异步隐藏启动阶段的直接子快照。
            var children = ListPool<UIBase>.Get();
            // 保证异步隐藏成功、失败或取消时都完成资源清理。
            try
            {
                // 捕获一次直接子UI关系并沿用到隐藏前流程结束。
                FindChildren(children, hideContext);
                // 子树联动或隐藏前通知使本轮失效时转入取消检查。
                if (!BeforeUIHideInvoke(children, operationVersion, hideContext, operationCts))
                    // 传播本轮版本失效或取消的具体原因。
                    ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);

                // 子树联动已同步结束，动画等待期间不保留层级引用或传递旧资格。
                hideContext.Dispose();

                // Before事件可能重入，执行动画前再次校验本轮身份。
                ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);
                // 等待自身隐藏表现，恢复阶段仍进入统一调度队列。
                await UIOperationDispatcher.Continue(OnUIHideAsync(operationCts.Token));

                // 处理器结束后再次阻止过期操作提交状态。
                ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);
                // 所有隐藏行为结束后提交隐藏完成状态。
                AfterUIHidedInvoke(operationVersion, operationCts);

                // After回调也可能触发外部取消，成功完成前必须再观察一次。
                ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);
            }
            catch (Exception operationException)
            {
                // 失败时取消本轮派生操作，同时保留原始异常和取消回调异常。
                CancelFailedShowHideAction(operationCts, operationException);
                // 保留异步隐藏的原始失败结果向调用方传播。
                throw;
            }
            finally
            {
                // 成功或失败都必须归还子UI快照并释放链接令牌注册。
                try
                {
                    // 归还本轮隐藏使用的直接子界面快照。
                    ListPool<UIBase>.Release(children);
                }
                finally
                {
                    // 即使结束隐藏活动登记异常，也必须释放链接取消源。
                    try
                    {
                        // 取消和故障同样结束隐藏占权，后续同组请求可以发起显式恢复。
                        CompleteCurrentHideAction(operationVersion);
                    }
                    finally
                    {
                        // 释放本轮链接令牌注册和取消源。
                        CompleteAsynchronousShowHideAction(operationCts);
                    }
                }
            }
        }

        /// <summary>
        ///  执行UI隐藏前的关系联动与生命周期通知。
        /// </summary>
        /// <param name="children">本轮捕获的直接子UI快照。</param>
        /// <param name="operationVersion">本轮显隐操作版本。</param>
        /// <param name="hideContext">当前递归隐藏保留的子树入口资格。</param>
        /// <param name="operationCts">异步流程的本轮取消源；同步流程传null。</param>
        /// <returns>本轮操作仍为最新请求时返回true。</returns>
        private bool BeforeUIHideInvoke(List<UIBase> children, int operationVersion, UIHideContext hideContext,
            CancellationTokenSource operationCts = null)
        {
            // 按快照顺序隐藏仍然属于当前UI的直接子节点。
            for (var i = 0; i < children.Count; i++)
            {
                // 读取快照中当前需要处理的直接子界面。
                var eachChild = children[i];
                // 跳过已销毁或已经离开当前父节点的子界面。
                if (!IsCurrentChild(eachChild)) continue;
                // 沿用本轮资格上下文递归执行子界面同步隐藏。
                eachChild.ExecuteHide(hideContext);

                // 子UI回调可能重入当前UI。
                if (!ValidateCurrentShowHideAction(operationVersion, operationCts)) return false;
            }

            // 先提交Hiding状态，再触发隐藏前事件。
            State = UIState.Hiding;
            // 状态通知可能使本轮失效，隐藏前事件之前再次检查。
            if (!ValidateCurrentShowHideAction(operationVersion, operationCts)) return false;
            // 按订阅快照派发隐藏前业务通知。
            UINotification.Invoke(this, nameof(BeforeUIHideEvent), BeforeUIHideEvent);
            // 返回隐藏前通知结束后的操作有效性。
            return ValidateCurrentShowHideAction(operationVersion, operationCts);
        }

        /// <summary>
        ///  提交UI隐藏完成状态并派发事件。
        /// </summary>
        /// <param name="operationVersion">本轮显隐操作版本。</param>
        /// <param name="operationCts">异步流程的本轮取消源；同步流程传null。</param>
        private void AfterUIHidedInvoke(int operationVersion, CancellationTokenSource operationCts = null)
        {
            // 过期操作不得覆盖更新请求的状态。
            if (!ValidateCurrentShowHideAction(operationVersion, operationCts)) return;
            // 所有隐藏表现完成后提交 Hided 状态。
            State = UIState.Hided;
            // 隐藏状态通知使本轮失效时不再派发成功事件。
            if (!ValidateCurrentShowHideAction(operationVersion, operationCts)) return;
            // 向业务报告本轮隐藏已经成功完成。
            UINotification.Invoke(this, nameof(AfterUIHidedEvent), AfterUIHidedEvent);
        }

        /// <summary>
        ///  执行UI显示前的关系联动与生命周期通知。
        /// </summary>
        /// <param name="siblings">用于保存本轮兄弟UI快照的列表。</param>
        /// <param name="parents">用于保存本轮祖先UI快照的列表。</param>
        /// <param name="operationVersion">本轮显隐操作版本。</param>
        /// <param name="showMode">当前显示请求的来源模式。</param>
        /// <returns>本轮操作仍为最新请求时返回true。</returns>
        private bool BeforeUIShowInvoke(List<UIBase> siblings, List<UIBase> parents, int operationVersion,
            UIShowMode showMode)
        {
            // 先提交Showing状态，确保后续回调观察到正确状态。
            State = UIState.Showing;
            // Showing 状态通知结束后复核当前显示准入。
            if (!MaintainCurrentShowAdmission(showMode, operationVersion)) return false;

            // 先允许事件调整层级，再捕获本轮关系快照。
            UINotification.Invoke(this, nameof(BeforeUIShowEvent), BeforeUIShowEvent);
            // 显示前业务通知结束后再次检查准入。
            if (!MaintainCurrentShowAdmission(showMode, operationVersion)) return false;

            // 事件回调可能换父或激活新兄弟，使用动态重新协调取代单次快照隐藏。
            if (!ReconcileCurrentShowSiblings(siblings, showMode, operationVersion)) return false;

            // 兄弟隐藏回调也可以换父，因此在它们完成后才捕获最新连续父UI链。
            if (ShouldAutoShowParentChain(showMode)) FindParents(parents);

            // 自动父链策略从根祖先到直接父UI依次处理，其他来源不主动改变父页面。
            if (ShouldAutoShowParentChain(showMode))
            {
                // 按从根到直接父界面的顺序补齐连续父链。
                for (var i = 0; i < parents.Count; i++)
                {
                    // 取得本轮快照中的下一个祖先界面。
                    var eachParent = parents[i];
                    // 跳过回调后已不再属于当前连续父链的节点。
                    if (!IsCurrentParent(eachParent)) continue;
                    // 仅对尚未完成关系调度的祖先发起内部显示。
                    if (!eachParent.IsShowRelationReady(true)) eachParent.ExecuteShow(allowScheduledParents: true);
                    // 祖先显示未能达到要求时停止当前目标的显示。
                    if (!MaintainAutoParentResult(eachParent, operationVersion)) return false;
                    // 每级祖先回调后复核目标当前显示资格。
                    if (!MaintainCurrentShowAdmission(showMode, operationVersion)) return false;
                }
                // 登记本轮自动父链已完成关系调度。
                _readyAutoParentChainVersion = operationVersion;
            }

            // 全部关系联动结束后最后确认本轮显示仍然拥有准入资格。
            return MaintainCurrentShowAdmission(showMode, operationVersion);
        }

        /// <summary>
        ///  异步执行UI显示前的关系联动与生命周期通知。
        /// </summary>
        /// <param name="siblings">用于保存本轮兄弟UI快照的列表。</param>
        /// <param name="parents">用于保存本轮祖先UI快照的列表。</param>
        /// <param name="operationVersion">本轮异步显示操作版本。</param>
        /// <param name="operationCts">本轮异步显示操作取消源。</param>
        /// <param name="showMode">当前显示请求的来源模式。</param>
        /// <returns>相邻UI联动全部结束后的任务。</returns>
        private async UniTask BeforeUIShowAsyncInvoke(List<UIBase> siblings, List<UIBase> parents,
            int operationVersion, CancellationTokenSource operationCts, UIShowMode showMode)
        {
            // 借用任务列表统一观察互斥隐藏和父链显示。
            var tasks = ListPool<UniTask>.Get();
            // 保证异步显示前联动结束或失败时归还任务列表。
            try
            {
                // 保存关系调度异常，随后仍观察已经启动的关联任务。
                Exception schedulingException = null;
                // 将启动阶段失败转为待收尾异常，避免中断已有任务观察。
                try
                {
                    // 提交 Showing 状态，开始本轮可观察的显示生命周期。
                    State = UIState.Showing;
                    // 状态变化通知后立即复核显示准入。
                    MaintainCurrentShowAdmission(showMode, operationVersion, operationCts);
                    // 派发显示前业务通知，允许业务提出后续请求。
                    UINotification.Invoke(this, nameof(BeforeUIShowEvent), BeforeUIShowEvent);
                    // 显示前通知结束后再次复核准入。
                    MaintainCurrentShowAdmission(showMode, operationVersion, operationCts);
                    // 将互斥兄弟的异步隐藏纳入关联任务集合。
                    tasks.Add(ReconcileCurrentShowSiblingsAsync(siblings, showMode, operationVersion, operationCts));
                    // 仅在普通自动父链策略下补齐连续祖先。
                    if (ShouldAutoShowParentChain(showMode))
                    {
                        // 按当前 Transform 层级重新捕获必须显示的父链。
                        FindParents(parents);
                        // 按从根到直接父界面的顺序同步启动祖先关系调度。
                        for (var i = 0; i < parents.Count; i++)
                        {
                            // 取得当前待调度的祖先界面。
                            var parent = parents[i];
                            // 跳过已离开当前连续祖先链的节点。
                            if (!IsCurrentParent(parent)) continue;
                            // 已完成关系调度的祖先无需重启动画。
                            if (!parent.IsShowRelationReady(true))
                                // 启动必要祖先的异步显示并跟踪其结果。
                                tasks.Add(parent.ExecuteShowAsync(operationCts.Token, allowScheduledParents: true));
                            // 验证该祖先已完成当前阶段所需的显示关系调度。
                            MaintainAutoParentResult(parent, operationVersion, operationCts);
                            // 祖先回调后继续检查目标版本和显示准入。
                            MaintainCurrentShowAdmission(showMode, operationVersion, operationCts);
                        }
                        // 将自动父链完成标记绑定到本轮版本。
                        _readyAutoParentChainVersion = operationVersion;
                    }
                    // 关系调度完成后再次确认目标可继续显示。
                    MaintainCurrentShowAdmission(showMode, operationVersion, operationCts);
                }
                catch (Exception exception)
                {
                    // 保存同步关系调度失败，以便完成已启动任务的收尾。
                    schedulingException = exception;
                }
                // 单独记录关联表现任务的失败。
                Exception presentationException = null;
                // 等待所有已启动父链与互斥任务后再传播错误。
                try { await UIOperationDispatcher.Continue(UIAsync.WaitAll(tasks)); }
                // 保存已启动关联任务收尾时报告的异常。
                catch (Exception exception) { presentationException = exception; }
                // 按统一规则传播调度异常和关联任务异常。
                ThrowHandlerExceptions(schedulingException, presentationException);
            }
            finally
            {
                // 归还显示前关系联动使用的任务列表。
                ListPool<UniTask>.Release(tasks);
            }
        }

        /// <summary>
        ///  同步显示配置为随父显示的直接子UI。
        /// </summary>
        /// <param name="children">用于保存本轮直接子UI快照的列表。</param>
        /// <param name="operationVersion">当前父UI的显隐操作版本。</param>
        /// <returns>当前父UI操作仍为最新请求时返回true。</returns>
        private bool ShowChildrenWithParentInvoke(List<UIBase> children, int operationVersion)
        {
            // 当前显示行为结束后再捕获直接子UI，允许行为在本轮中调整层级。
            FindChildrenForParentShow(children);

            // 按Hierarchy顺序逐个显示，保证互斥组处理与生命周期顺序确定。
            for (var i = 0; i < children.Count; i++)
            {
                // 取出当前随父显示候选子界面。
                var eachChild = children[i];

                // 仅处理仍属于当前UI且明确配置为随父显示的直接子UI。
                if (!IsCurrentChildForParentShow(eachChild) || !eachChild.showWithParent) continue;

                // 已初始化且已显示的节点无需重复执行；未初始化初态仍需恢复实际效果。
                if (eachChild._isUIStateInitialized && eachChild.State is UIState.Showed) continue;

                // 显式Showing请求必须继续等待当前父UI。
                if (eachChild.State is UIState.Showing && eachChild.IsCurrentNormalShowAction()) continue;

                // 同组兄弟正在显式显示时让显式请求优先，避免默认子UI反向取代请求来源。
                if (HasShowingUIInSiblingGroup(children, eachChild)) continue;

                // 同一父级联批次预先跳过严格低优先级候选，避免结果受Hierarchy顺序影响。
                if (HasHigherPriorityShowWithParentCandidate(children, eachChild)) continue;

                // 使用专用入口执行完整子UI生命周期，同时省略再次向上显示当前父UI。
                eachChild.ShowFromParent(this, operationVersion);

                // 子UI生命周期可能通过回调隐藏、销毁或重新显示当前父UI。
                if (!IsCurrentShowHideAction(operationVersion)) return false;
            }

            // 没有匹配子UI时同样需要确认当前父UI操作仍然有效。
            return IsCurrentShowHideAction(operationVersion);
        }

        /// <summary>
        ///  异步显示配置为随父显示的直接子UI。
        /// </summary>
        /// <param name="children">用于保存本轮直接子UI快照的列表。</param>
        /// <param name="operationVersion">当前父UI的显隐操作版本。</param>
        /// <param name="operationCts">当前父UI本轮显示操作的取消源。</param>
        /// <returns>全部已调度目标子UI的表现结束后的任务。</returns>
        private async UniTask ShowChildrenWithParentAsyncInvoke(List<UIBase> children, int operationVersion,
            CancellationTokenSource operationCts)
        {
            // 借用列表跟踪本轮实际启动的默认子界面任务。
            var tasks = ListPool<UniTask>.Get();
            // 确保异步子界面联动结束或失败时释放任务列表。
            try
            {
                // 记录子界面启动阶段的调度异常。
                Exception schedulingException = null;
                // 捕获启动阶段错误，保留此前已启动子界面的观察流程。
                try
                {
                    // 同步调度阶段捕获直接子UI；动画结束后不再发现新节点。
                    FindChildrenForParentShow(children);

                    // 按Hierarchy顺序启动关系调度，不等待上一个子UI动画。
                    for (var i = 0; i < children.Count; i++)
                    {
                        // 每次访问快照节点前都确认父UI仍由本轮操作控制。
                        ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);
                        // 取得当前随父显示候选子界面。
                        var eachChild = children[i];

                        // 仅处理仍属于当前UI且明确配置为随父显示的直接子UI。
                        if (!IsCurrentChildForParentShow(eachChild) || !eachChild.showWithParent) continue;

                        // 已初始化且已显示的节点无需重复执行；未初始化初态仍需恢复实际效果。
                        if (eachChild._isUIStateInitialized && eachChild.State is UIState.Showed) continue;

                        // 显式Showing请求必须继续等待当前父UI。
                        if (eachChild.State is UIState.Showing && eachChild.IsCurrentNormalShowAction()) continue;

                        // 同组兄弟正在显式显示时让显式请求优先，避免默认面板取代请求来源。
                        if (HasShowingUIInSiblingGroup(children, eachChild)) continue;

                        // 父级联批次先选中严格更高优先级候选，不启动注定会被抢占的低优先级异步行为。
                        if (HasHigherPriorityShowWithParentCandidate(children, eachChild, chooseLastEqual: true)) continue;

                        // 子UI继承父UI令牌，同步启动关系调度，表现阶段不阻塞其他子UI。
                        tasks.Add(eachChild.ShowFromParentAsync(operationCts.Token, this, operationVersion));

                        // 子UI回调可能取代当前父UI操作，继续处理下一个节点前必须再次校验。
                        ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);
                    }
                }
                // 保存随父显示调度中断的异常。
                catch (Exception exception) { schedulingException = exception; }
                // 单独记录已启动子界面表现的失败。
                Exception presentationException = null;
                // 等待所有已启动子界面任务完成收尾。
                try { await UIOperationDispatcher.Continue(UIAsync.WaitAll(tasks)); }
                // 保存子界面任务完成时产生的异常。
                catch (Exception exception) { presentationException = exception; }
                // 合并或保留堆栈传播子界面调度与表现错误。
                ThrowHandlerExceptions(schedulingException, presentationException);
            }
            // 归还本轮默认子界面任务列表。
            finally { ListPool<UniTask>.Release(tasks); }
        }

        /// <summary>
        ///  判断指定子UI是否存在同互斥组且包含有效普通显示请求的兄弟UI分支。
        /// </summary>
        /// <param name="children">当前父UI本轮捕获的直接子UI快照。</param>
        /// <param name="targetChild">准备由父UI自动显示的目标子UI。</param>
        /// <returns>存在同组兄弟分支包含有效普通显示请求时返回true。</returns>
        private bool HasShowingUIInSiblingGroup(List<UIBase> children, UIBase targetChild)
        {
            // 只检查同一份直接子UI快照，避免级联过程中重复遍历Transform。
            for (var i = 0; i < children.Count; i++)
            {
                // 取得同批候选列表中的兄弟分支。
                var eachSibling = children[i];

                // 目标自身、失效节点和已经离开当前父UI的节点都不参与判断。
                if (eachSibling == targetChild || !IsCurrentChildForParentShow(eachSibling)) continue;

                // 同互斥组兄弟分支中的任意有效普通显示请求都可能被隐藏父分支间接取消。
                if (eachSibling.mutexGroup == targetChild.mutexGroup &&
                    // 同组分支存在显式显示请求时保留其显示权。
                    HasShowingUIInBranch(eachSibling)) return true;
            }

            // 没有竞争中的同组兄弟分支时允许目标子UI按原互斥规则显示。
            return false;
        }

        /// <summary>
        ///  判断目标子UI是否存在同组且配置为随父显示的严格更高优先级候选。
        /// </summary>
        /// <param name="children">当前父UI本轮捕获的直接子UI快照。</param>
        /// <param name="targetChild">准备由父UI自动显示的目标子UI。</param>
        /// <returns>存在严格更高优先级候选时返回true。</returns>
        /// <param name="chooseLastEqual">同优先级时是否选择当前层级顺序中最后一个候选。</param>
        private bool HasHigherPriorityShowWithParentCandidate(List<UIBase> children, UIBase targetChild,
            bool chooseLastEqual = false)
        {
            // 候选预选只比较同一份直接子UI快照，不扩大到其他层级。
            for (var i = 0; i < children.Count; i++)
            {
                // 取得参与默认子界面优先级比较的候选。
                var eachCandidate = children[i];

                // 目标自身、已失效节点和未配置随父显示的UI都不是本批次候选。
                if (eachCandidate == targetChild ||
                    !IsCurrentChildForParentShow(eachCandidate) ||
                    // 不符合当前父级联资格的候选不参与优先级仲裁。
                    !eachCandidate.showWithParent) continue;

                // 只比较同互斥组；异步批次预选同优先级的最后一个，避免启动后立即取消。
                if (eachCandidate.mutexGroup == targetChild.mutexGroup &&
                    (eachCandidate.mutexPriority > targetChild.mutexPriority ||
                     chooseLastEqual && eachCandidate.mutexPriority == targetChild.mutexPriority &&
                     // 找到严格更高优先级或按规则胜出的同优先级候选。
                     i > children.IndexOf(targetChild))) return true;
            }

            // 本批次没有严格更高优先级候选时允许目标继续原流程。
            return false;
        }

        /// <summary>
        ///  判断连续UI子树分支中是否存在仍在执行的显式显示请求。
        /// </summary>
        /// <param name="branchUI">需要检查的分支根UI。</param>
        /// <returns>分支根或连续UI后代存在有效普通显示请求时返回true。</returns>
        private static bool HasShowingUIInBranch(UIBase branchUI)
        {
            // 已销毁分支不再包含有效显示请求。
            if (!branchUI) return false;

            // 使用池化列表作为待检查栈，避免深层UI结构产生方法递归。
            var pendingUIs = ListPool<UIBase>.Get();
            // 无论提前找到显式请求还是完整扫描，都归还遍历栈。
            try
            {
                // 将待检查分支根节点放入显式请求扫描栈。
                pendingUIs.Add(branchUI);
                // 逐个检查尚未处理的连续 UI 分支节点。
                while (pendingUIs.Count > 0)
                {
                    // 从列表末尾取出一个分支节点，并立即移除已经消费的引用。
                    var lastIndex = pendingUIs.Count - 1;
                    // 取得当前栈顶分支界面。
                    var currentUI = pendingUIs[lastIndex];
                    // 弹出已取出的节点，避免后续重复消费。
                    pendingUIs.RemoveAt(lastIndex);

                    // 回调期间销毁的节点直接跳过，有效普通显示预约立即确认存在竞争请求。
                    if (!currentUI) continue;
                    // 当前节点持有有效普通显示预约时立即确认存在竞争请求。
                    if (currentUI.IsCurrentNormalShowAction()) return true;

                    // 只沿直接挂载UIBase的连续Transform链向下检查。
                    for (var i = 0; i < currentUI.transform.childCount; i++)
                    {
                        // 取得当前分支的下一个直接子 Transform。
                        var child = currentUI.transform.GetChild(i);
                        // 仅将直接挂载 UIBase 的子节点加入待检查栈。
                        if (child.TryGetComponent<UIBase>(out var childUI)) pendingUIs.Add(childUI);
                    }
                }

                // 当前连续UI分支没有正在处理的显式显示请求。
                return false;
            }
            finally
            {
                // 提前找到Showing节点或完整扫描结束时都归还临时栈。
                ListPool<UIBase>.Release(pendingUIs);
            }
        }

        /// <summary>
        ///  提交UI显示完成状态、调整兄弟层级并派发事件。
        /// </summary>
        /// <param name="siblings">本轮动态协调后保留的最新兄弟UI快照。</param>
        /// <param name="operationVersion">本轮显隐操作版本。</param>
        /// <param name="showMode">当前显示请求的来源模式。</param>
        /// <param name="operationCts">异步流程的本轮取消源；同步流程传null。</param>
        /// <param name="updateSiblingIndex">是否在显示完成阶段调整兄弟层级顺序。</param>
        private void AfterUIShowedInvoke(List<UIBase> siblings, int operationVersion, UIShowMode showMode,
            CancellationTokenSource operationCts = null, bool updateSiblingIndex = true)
        {
            // 提交Showed前最后复核父链和兄弟优先级，失去准入时由隐藏流程收口。
            if (!MaintainCurrentShowAdmission(showMode, operationVersion, operationCts)) return;
            // 在表现和关联任务完成后提交 Showed 状态。
            State = UIState.Showed;
            // 状态通知使本轮失效时停止派发显示成功事件。
            if (!ValidateCurrentShowHideAction(operationVersion, operationCts)) return;

            // 仅在调用阶段要求时调整兄弟层级顺序。
            if (updateSiblingIndex) BringToFront(siblings);

            // 状态通知和置顶回调可能改变关系，成功通知前按当前阶段重新复核。
            if (operationCts == null)
            {
                // 同步调度仍可协调新兄弟，完成互斥处理后再报告成功。
                if (!ReconcileCurrentShowSiblings(siblings, showMode, operationVersion)) return;
            }
            else
            {
                // 异步完成阶段只核验已提交关系，不重新补父链或隐藏新兄弟。
                MaintainCommittedShowRelations(siblings, showMode, operationVersion, operationCts);
            }
            // 所有完成阶段复核通过后派发显示成功通知。
            UINotification.Invoke(this, nameof(AfterUIShowedEvent), AfterUIShowedEvent);
        }

        /// <summary>依据当前有效兄弟的层级位置，将可移动面板置于最前。</summary>
        /// <param name="siblings">接收或保存本轮兄弟界面关系的列表。</param>
        private void BringToFront(List<UIBase> siblings)
        {
            // 固定层级的UI不参与自动置顶。
            if (!fixedSiblingIndex)
            {
                // 以自身当前层级位置初始化可置顶范围。
                var maxSiblingIndex = transform.GetSiblingIndex();
                // 记录是否确实存在可参与置顶的非固定兄弟。
                var hasFlexibleSibling = false;
                // 检查当前兄弟快照中的可移动节点。
                for (var i = 0; i < siblings.Count; i++)
                {
                    // 取得当前待检查的兄弟界面。
                    var eachSibling = siblings[i];
                    // 排除关系已失效或固定层级的兄弟。
                    if (!IsCurrentSibling(eachSibling) || eachSibling.fixedSiblingIndex) continue;
                    // 标记当前面板存在可相对调整的兄弟。
                    hasFlexibleSibling = true;
                    // 记录所有有效可移动兄弟中的最大层级序号。
                    maxSiblingIndex = Mathf.Max(maxSiblingIndex, eachSibling.transform.GetSiblingIndex());
                }

                // 至少存在一个可变兄弟时才调整层级。
                if (hasFlexibleSibling) transform.SetSiblingIndex(maxSiblingIndex);
            }
        }

        /// <summary>
        ///  为仍然有效的显示操作登记来源模式和对应版本。
        /// </summary>
        /// <param name="showMode">当前显示请求的来源模式。</param>
        /// <param name="operationVersion">当前显示请求的显隐操作版本。</param>
        /// <param name="parentCascadeSource">父级联请求的直接发起父UI；普通显示传null。</param>
        /// <param name="parentCascadeSourceOperationVersion">来源父UI显示事务版本；普通显示传-1。</param>
        private void RegisterCurrentShowMode(UIShowMode showMode, int operationVersion,
            UIBase parentCascadeSource, int parentCascadeSourceOperationVersion)
        {
            // 取消旧操作时的回调可能已经登记新版本，旧流程不得覆盖新请求来源。
            if (!IsCurrentShowHideAction(operationVersion)) return;

            // 模式与版本成对提交，使后续Showing判断只识别当前显示请求。
            _currentShowMode = showMode;
            // 将显示来源模式绑定到当前显隐操作版本。
            _showModeOperationVersion = operationVersion;
            // 登记该版本仍拥有有效显示预约。
            _activeShowOperationVersion = operationVersion;

            // 只有父级联模式保留来源父UI，普通请求必须清除上一轮级联引用。
            _currentParentCascadeSource = showMode is UIShowMode.ParentCascade
                ? parentCascadeSource
                : null;
            // 父级联保存来源父操作版本，普通显示清除该来源版本。
            _currentParentCascadeSourceOperationVersion = showMode is UIShowMode.ParentCascade
                ? parentCascadeSourceOperationVersion
                : -1;
        }

        /// <summary>
        ///  结束指定版本的显示生命周期活动登记。
        /// </summary>
        /// <param name="operationVersion">已经离开显示生命周期的显隐操作版本。</param>
        private void CompleteCurrentShowAction(int operationVersion)
        {
            // 旧操作的finally不得清除更新请求刚登记的活动版本。
            if (_activeShowOperationVersion != operationVersion) return;

            // Showing状态可以因异常保留，但结束后的请求不再具有显式显示优先级。
            _activeShowOperationVersion = -1;

            // 当前显示生命周期结束后不再保留可能已销毁的父级联来源引用。
            _currentParentCascadeSource = null;
            // 清除已完成显示所保存的父级联来源版本。
            _currentParentCascadeSourceOperationVersion = -1;
        }

        /// <summary>
        ///  为仍然有效的隐藏操作登记活动版本。
        /// </summary>
        /// <param name="operationVersion">当前隐藏请求的显隐操作版本。</param>
        private void RegisterCurrentHideAction(int operationVersion)
        {
            // 取消旧操作的回调可能已经登记更新版本，旧隐藏不得覆盖它。
            if (!IsCurrentShowHideAction(operationVersion)) return;

            // 活动版本只用于识别本轮真正执行中的隐藏生命周期。
            _activeHideOperationVersion = operationVersion;
        }

        /// <summary>
        ///  结束指定版本的隐藏生命周期活动登记。
        /// </summary>
        /// <param name="operationVersion">已经离开隐藏生命周期的显隐操作版本。</param>
        private void CompleteCurrentHideAction(int operationVersion)
        {
            // 旧隐藏的finally不得清除更新隐藏刚登记的活动版本。
            if (_activeHideOperationVersion != operationVersion) return;

            // 失败或取消可以残留Hiding状态，但已结束操作不再持有组级优先权。
            _activeHideOperationVersion = -1;
        }

        /// <summary>
        ///  判断当前Showing状态是否属于仍然有效的普通显示请求。
        /// </summary>
        /// <returns>当前操作由公开API或序列化初态发起且仍为最新版本时返回true。</returns>
        private bool IsCurrentNormalShowAction()
        {
            // 显示请求可能尚处于Hided入口预约窗口，因此不以State作为有效性前提。
            if (_currentShowMode is not UIShowMode.Normal) return false;

            // 普通显示还必须同时是当前有效显示预约。
            return IsCurrentShowAction();
        }

        /// <summary>
        ///  判断当前是否存在仍然有效的显示操作预约。
        /// </summary>
        /// <returns>显示版本仍为最新且未被取消时返回true。</returns>
        private bool IsCurrentShowAction()
        {
            // 显示活动版本、模式版本和全局显隐版本必须指向同一请求。
            if (_activeShowOperationVersion != _showHideOperationVersion ||
                // 显示活动版本或模式版本过期时不再拥有显示预约。
                !IsCurrentShowHideAction(_showModeOperationVersion)) return false;

            // 外部取消已同步到链接取消源时，不再让过期请求占用互斥组。
            return _currentShowHideCts == null || !_currentShowHideCts.IsCancellationRequested;
        }

        /// <summary>
        ///  判断当前是否存在仍然有效的隐藏操作。
        /// </summary>
        /// <returns>隐藏活动版本仍为最新且未被取消时返回true。</returns>
        private bool IsCurrentHideAction()
        {
            // 只有与全局显隐版本一致的隐藏生命周期才能继续占权。
            if (_activeHideOperationVersion != _showHideOperationVersion) return false;

            // 异步隐藏的链接令牌已取消时，允许后续请求通过新隐藏流程恢复残留状态。
            return _currentShowHideCts == null || !_currentShowHideCts.IsCancellationRequested;
        }

        /// <summary>
        ///  开始一次同步显隐操作，并取消此前异步操作。
        /// </summary>
        /// <param name="showMode">显示操作的来源模式；隐藏操作传null。</param>
        /// <param name="parentCascadeSource">父级联请求的直接发起父UI；其他操作传null。</param>
        /// <param name="parentCascadeSourceOperationVersion">来源父UI显示事务版本；其他操作传-1。</param>
        /// <returns>本轮同步显隐操作版本。</returns>
        private int BeginSynchronousShowHideAction(UIShowMode? showMode = null,
            UIBase parentCascadeSource = null, int parentCascadeSourceOperationVersion = -1)
        {
            // 先摘除旧取消源，避免旧finally误清理后来创建的新操作。
            var previousCts = _currentShowHideCts;
            // 先断开旧取消源引用，避免旧操作清理较新的操作。
            _currentShowHideCts = null;

            // 显示操作先登记软预约，使取消旧令牌产生的重入请求能观察当前优先级。
            var operationVersion = NextShowHideOperationVersion();
            // 显示方向需要在取消旧操作前登记入口预约。
            if (showMode.HasValue)
                // 保存本轮显示来源、版本和父级联上下文。
                RegisterCurrentShowMode(
                    showMode.Value,
                    operationVersion,
                    parentCascadeSource,
                    parentCascadeSourceOperationVersion);
            // 取消旧操作时保留新版本，并在失败时撤销未启动的显示预约。
            try
            {
                // 取消回调中的更新请求仍可以用更新版本取代本轮操作。
                previousCts?.Cancel();
                // 返回已登记的本轮同步操作版本。
                return operationVersion;
            }
            catch (Exception)
            {
                // 取消回调抛错时结束本轮未启动的显示预约，避免永久占用互斥组。
                if (showMode.HasValue) CompleteCurrentShowAction(operationVersion);
                // 原样传播旧操作取消回调产生的异常。
                throw;
            }
        }

        /// <summary>
        ///  开始一次异步显隐操作，并以局部取消源替换此前操作。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌。</param>
        /// <param name="operationVersion">返回本轮显隐操作版本。</param>
        /// <param name="showMode">显示操作的来源模式；隐藏操作传null。</param>
        /// <param name="parentCascadeSource">父级联请求的直接发起父UI；其他操作传null。</param>
        /// <param name="parentCascadeSourceOperationVersion">来源父UI显示事务版本；其他操作传-1。</param>
        /// <returns>仅属于本轮操作的链接取消源。</returns>
        private CancellationTokenSource BeginAsynchronousShowHideAction(CancellationToken cancellationToken,
            out int operationVersion, UIShowMode? showMode = null, UIBase parentCascadeSource = null,
            int parentCascadeSourceOperationVersion = -1)
        {
            // 任何可见副作用发生前观察取消。
            ThrowIfShowHideCancellationRequested(cancellationToken);
            // 将调用方取消和 Unity 对象销毁关联到本轮操作。
            var operationCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, destroyCancellationToken);

            // 先登记新操作，再取消旧操作，保证最后请求获胜。
            var previousCts = _currentShowHideCts;
            // 分配新的显隐版本，使此前操作立即过期。
            operationVersion = NextShowHideOperationVersion();
            // 将本轮链接取消源登记为当前操作。
            _currentShowHideCts = operationCts;
            // 显示方向在取消旧操作前登记新的入口预约。
            if (showMode.HasValue)
                // 保存本轮异步显示来源与父级联版本。
                RegisterCurrentShowMode(
                    showMode.Value,
                    operationVersion,
                    parentCascadeSource,
                    parentCascadeSourceOperationVersion);
            // 保证启动过程失败时释放刚创建的取消源。
            try
            {
                // 取消此前操作，并同步执行其取消回调。
                previousCts?.Cancel();
                // 旧取消回调结束后检查本轮是否仍是最新且未取消的操作。
                ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);
                // 返回只属于本轮异步生命周期的取消源。
                return operationCts;
            }
            catch (Exception)
            {
                // 启动失败时只清理由本方法登记的取消源。
                if (ReferenceEquals(_currentShowHideCts, operationCts)) _currentShowHideCts = null;
                // 启动失败的显示不得继续持有入口预约。
                if (showMode.HasValue) CompleteCurrentShowAction(operationVersion);
                // 释放未能成功启动的链接取消源。
                operationCts.Dispose();
                // 将启动失败的原始异常继续传播。
                throw;
            }
        }

        /// <summary>
        ///  完成异步显隐操作并释放链接令牌注册。
        /// </summary>
        /// <param name="operationCts">本轮局部取消源。</param>
        private void CompleteAsynchronousShowHideAction(CancellationTokenSource operationCts)
        {
            // 旧finally不能清空更新操作的取消源。
            if (ReferenceEquals(_currentShowHideCts, operationCts)) _currentShowHideCts = null;
            // 解除当前操作持有的链接令牌注册。
            operationCts.Dispose();
        }

        /// <summary>
        ///  异步显隐失败时取消其派生操作，并保留取消回调产生的附加异常。
        /// </summary>
        /// <param name="operationCts">本轮局部取消源。</param>
        /// <param name="operationException">触发失败清理的原始异常。</param>
        /// <exception cref="AggregateException">原始异常与取消回调异常同时存在时抛出。</exception>
        private static void CancelFailedShowHideAction(CancellationTokenSource operationCts,
            Exception operationException)
        {
            // 已取消的来源不重复Cancel，避免重复执行第三方取消回调。
            if (operationCts.IsCancellationRequested) return;

            // 捕获取消派生任务时的附加异常，保留最初失败原因。
            try
            {
                // 通知本轮派生任务停止，避免异常路径留下后台副作用。
                operationCts.Cancel();
            }
            catch (Exception cancelException)
            {
                // 取消回调违反约定抛错时，同时保留原始业务异常用于诊断。
                throw new AggregateException(operationException, cancelException);
            }
        }

        /// <summary>
        ///  校验异步显隐操作仍然有效。
        /// </summary>
        /// <param name="operationCts">本轮局部取消源。</param>
        /// <param name="operationVersion">本轮显隐操作版本。</param>
        /// <exception cref="OperationCanceledException">操作已取消或被取代时抛出。</exception>
        private void ThrowIfCurrentShowHideActionInvalid(CancellationTokenSource operationCts,
            int operationVersion)
        {
            // 优先传播具有准确来源令牌的取消异常。
            operationCts.Token.ThrowIfCancellationRequested();
            // 引用和版本均匹配时确认该异步操作仍有效。
            if (ReferenceEquals(_currentShowHideCts, operationCts) &&
                // 当前操作仍有效，无需生成取消异常。
                IsCurrentShowHideAction(operationVersion)) return;

            // 将版本替代导致的失效报告为本轮操作取消。
            throw new OperationCanceledException("当前UI显隐操作已被更新操作取代。", null, operationCts.Token);
        }

        /// <summary>
        ///  同时观察调用方取消和Unity对象销毁。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌。</param>
        /// <exception cref="OperationCanceledException">任一令牌已取消时抛出。</exception>
        private void ThrowIfShowHideCancellationRequested(CancellationToken cancellationToken)
        {
            // 调用方取消具有更直接的错误来源，优先检查。
            cancellationToken.ThrowIfCancellationRequested();
            // 对象已销毁时以取消结束生命周期。
            if (!this) throw new OperationCanceledException("UI对象已销毁。");
            // 继续观察 Unity 对象销毁对应的取消令牌。
            destroyCancellationToken.ThrowIfCancellationRequested();
        }

        /// <summary>
        ///  获取下一个显隐操作版本。
        /// </summary>
        /// <returns>新的显隐操作版本。</returns>
        private int NextShowHideOperationVersion()
        {
            // 整数允许自然回绕，实际UI生命周期不会跨越完整整数空间。
            unchecked
            {
                // 递增显隐版本，令之前的操作失效。
                _showHideOperationVersion++;
            }

            // 返回分配给新操作的版本号。
            return _showHideOperationVersion;
        }

        /// <summary>
        ///  判断指定版本是否仍为当前显隐操作。
        /// </summary>
        /// <param name="operationVersion">需要校验的操作版本。</param>
        /// <returns>仍为最新操作时返回true。</returns>
        private bool IsCurrentShowHideAction(int operationVersion)
        {
            // 同时阻止已销毁组件和事件重入后的旧版本继续访问Transform或提交状态。
            return this && operationVersion == _showHideOperationVersion;
        }

        /// <summary>
        ///  校验同步或异步显隐流程是否仍可继续提交生命周期。
        /// </summary>
        /// <param name="operationVersion">需要校验的操作版本。</param>
        /// <param name="operationCts">异步流程的本轮取消源；同步流程传null。</param>
        /// <returns>同步流程仍为最新操作时返回true；异步流程无效时直接抛取消异常。</returns>
        private bool ValidateCurrentShowHideAction(int operationVersion, CancellationTokenSource operationCts)
        {
            // 同步流程没有局部取消源，只需要用版本阻止事件重入覆盖。
            if (operationCts == null) return IsCurrentShowHideAction(operationVersion);

            // 异步流程必须同时满足令牌、字段身份和版本校验。
            ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);
            // 同步或异步的版本与取消检查通过后确认操作有效。
            return true;
        }

        /// <summary>
        ///  在全部已启动处理器结束后传播同步调用异常和异步任务异常。
        /// </summary>
        /// <param name="invocationException">处理器返回UniTask之前同步抛出的异常。</param>
        /// <param name="taskException">已启动处理器任务产生的异常。</param>
        /// <exception cref="AggregateException">同步和异步路径同时失败时抛出。</exception>
        private static void ThrowHandlerExceptions(Exception invocationException, Exception taskException)
        {
            // 两条路径都只表示取消时保持取消语义，不包装为故障聚合。
            if (invocationException is OperationCanceledException &&
                taskException is OperationCanceledException)
            {
                // 同一个异常已被两条路径观察时只按原堆栈抛出一次。
                ExceptionDispatchInfo.Capture(invocationException).Throw();
                // 单个异常传播后无需继续构造聚合错误。
                return;
            }

            // 两条路径同时失败且至少包含业务异常时保留全部原因。
            if (invocationException != null && taskException != null)
                // 两个不同阶段均失败时保留两个原始异常。
                throw new AggregateException(invocationException, taskException);

            // 单一异常使用ExceptionDispatchInfo保留原始调用栈。
            if (invocationException != null)
                // 仅启动阶段失败时按原始堆栈传播该异常。
                ExceptionDispatchInfo.Capture(invocationException).Throw();
            // 仅存在任务等待异常时继续传播该结果。
            if (taskException != null)
                // 保留任务失败的原始抛出位置。
                ExceptionDispatchInfo.Capture(taskException).Throw();
        }

        /// <summary>
        ///  根据序列化状态初始化UI显隐。
        /// </summary>
        private void InitializeUIState()
        {
            // 销毁或已经完成初始化的面板不重复处理初态。
            if (!this || _isUIStateInitialized) return;
            // 首次初始化开始即登记，避免重入重复执行。
            _isUIStateInitialized = true;
            // 按当前序列化状态补齐相应生命周期表现。
            InitUIState();
        }

        /// <summary>根据序列化状态和父链准入完成首次显隐初始化。</summary>
        private void InitUIState()
        {
            // 序列化Showed只在当前关系满足准入时恢复实际显示效果。
            if (State == UIState.Showed)
            {
                // 严格父链未就绪或更高优先级已占权时，将不可见假状态规范化为Hided。
                if (!CanPreflightShow()) HideNotCheckState();
                // 已显示初态通过准入后补齐同步显示生命周期。
                else ShowNotCheckState(UIShowMode.Normal);
            }
            // 已隐藏初态执行同步隐藏以同步表现和子界面。
            else if (State == UIState.Hided) HideNotCheckState();
            // Showing 初态先收口，再以正常异步显示入口启动。
            else if (State == UIState.Showing)
            {
                // 过渡初态先收口为Hided，再在当前初始化调度中重新执行异步准入。
                HideNotCheckState();
                // 将初始显示过渡与对象销毁令牌关联并启动。
                ExecuteShowAsync(destroyCancellationToken).Forget();
            }
            // Hiding 初态先同步显示，再启动正常异步隐藏流程。
            else if (State == UIState.Hiding)
            {
                // 不具备显示准入时直接完成隐藏，避免启动一次必然被拒绝的显示。
                if (!CanPreflightShow()) HideNotCheckState();
                else
                {
                    // 兼容原有Hiding初态，先恢复显示效果再异步完成隐藏。
                    ShowNotCheckState(UIShowMode.Normal);
                    // 启动与对象销毁令牌关联的初始隐藏过渡。
                    ExecuteHideAsync(destroyCancellationToken).Forget();
                }
            }
        }

        /// <summary>
        ///  将连续UI祖先按根祖先到直接父UI的顺序写入快照。
        /// </summary>
        /// <param name="parents">接收祖先UI的可复用列表。</param>
        private void FindParents(List<UIBase> parents)
        {
            // 池中列表写入前必须清空旧数据。
            parents.Clear();

            // 从直接父节点向上扫描，遇到没有UIBase的节点即终止UI关系链。
            var current = transform.parent;
            // 沿当前连续父 Transform 链逐层向上扫描。
            while (current)
            {
                // 遇到未挂载 UIBase 的普通 Transform 即停止父链扫描。
                if (!current.TryGetComponent<UIBase>(out var parentUI)) break;
                // 将当前祖先加入待调度父链。
                parents.Add(parentUI);
                // 继续检查更上一层 Transform。
                current = current.parent;
            }

            // 显示时必须先保证根祖先完成，再处理更接近当前UI的父节点。
            parents.Reverse();
        }

        /// <summary>
        ///  将当前需要参与显示关系联动的兄弟UI写入指定快照列表。
        /// </summary>
        /// <param name="siblings">接收兄弟UI的可复用列表。</param>
        private void FindSiblings(List<UIBase> siblings)
        {
            // 池中列表写入前必须清空旧数据。
            siblings.Clear();
            // 读取当前直接父节点以确定兄弟扫描范围。
            var parent = transform.parent;
            // 存在父节点时只在该父节点的直接子节点中查找兄弟。
            if (parent)
            {
                // 非根节点只扫描父Transform的直接子节点。
                for (var i = 0; i < parent.childCount; i++)
                {
                    // 取得当前待检查的直接子 Transform。
                    var child = parent.GetChild(i);
                    // 排除自身和未挂载 UIBase 的节点。
                    if (child == transform || !child.TryGetComponent<UIBase>(out var sibling)) continue;

                    // 实际显示联动除活跃节点外，还必须能取消尚未激活的显示预约。
                    if (child.gameObject.activeInHierarchy || sibling.IsCurrentShowAction())
                        // 将当前活动或持有有效预约的兄弟加入快照。
                        siblings.Add(sibling);
                }

                // 直属兄弟扫描完成后不再扫描场景根对象。
                return;
            }

            // 根节点关系限定在当前已加载Scene内。
            var scene = gameObject.scene;
            // 场景尚未加载时没有可参与调度的根兄弟。
            if (!scene.isLoaded) return;
            // 借用 GameObject 列表保存场景根节点。
            var rootObjects = ListPool<GameObject>.Get();
            // 保证场景根兄弟扫描结束后归还临时列表。
            try
            {
                // 读取当前场景的全部根 GameObject。
                scene.GetRootGameObjects(rootObjects);
                // 逐个检查场景根对象中的兄弟界面。
                for (var i = 0; i < rootObjects.Count; i++)
                {
                    // 取得当前待检查的根对象。
                    var rootObject = rootObjects[i];
                    // 排除自身和没有 UIBase 的场景根节点。
                    if (rootObject == gameObject ||
                        // 当前根节点不是有效兄弟，继续检查后续对象。
                        !rootObject.TryGetComponent<UIBase>(out var sibling)) continue;

                    // 根UI的入口预约与普通兄弟采用相同取消规则。
                    if (rootObject.activeInHierarchy || sibling.IsCurrentShowAction())
                        // 将当前活动或持有有效预约的根兄弟加入快照。
                        siblings.Add(sibling);
                }
            }
            finally
            {
                // 归还Scene根对象临时列表。
                ListPool<GameObject>.Release(rootObjects);
            }
        }

        /// <summary>
        ///  将当前需要参与父隐藏联动的直接子UI写入指定快照列表。
        /// </summary>
        /// <param name="children">接收直接子UI的可复用列表。</param>
        /// <param name="hideContext">补充本轮较早回调失活前已经取得的父子关系资格。</param>
        private void FindChildren(List<UIBase> children, UIHideContext hideContext)
        {
            // 只扫描直接子Transform，避免遍历整棵子树。
            children.Clear();
            // 按当前层级顺序扫描直接子 Transform。
            for (var i = 0; i < transform.childCount; i++)
            {
                // 取得当前隐藏候选子 Transform。
                var child = transform.GetChild(i);
                // 普通 Transform 不参与直接 UI 父子隐藏联动。
                if (!child.TryGetComponent<UIBase>(out var childUI)) continue;

                // 父隐藏除活跃子UI外，还需取消尚未激活GameObject的显示预约。
                if (child.gameObject.activeInHierarchy || childUI.IsCurrentShowAction() ||
                    hideContext.Contains(this, childUI))
                {
                    // 将当前合格直接子界面加入本层隐藏快照。
                    children.Add(childUI);
                    // 保留原扫描时点对新活动节点的发现，并在其回调前补齐新分支的资格。
                    hideContext.Include(this, childUI);
                }
            }
        }

        /// <summary>
        ///  将可能随父UI显示的直接子UI写入指定快照列表。
        /// </summary>
        /// <param name="children">接收直接子UI的可复用列表。</param>
        private void FindChildrenForParentShow(List<UIBase> children)
        {
            // 父显示需要发现由HideActions主动失活的子节点，因此不检查activeInHierarchy。
            children.Clear();
            // 按当前层级顺序发现可能随父显示的子界面。
            for (var i = 0; i < transform.childCount; i++)
            {
                // 取得当前待检查的直接子 Transform。
                var child = transform.GetChild(i);
                // 记录直接挂载 UIBase 的候选，不因隐藏表现失活而排除。
                if (child.TryGetComponent<UIBase>(out var childUI)) children.Add(childUI);
            }
        }

        /// <summary>
        ///  判断快照节点当前是否仍是有效兄弟UI。
        /// </summary>
        /// <param name="sibling">需要校验的兄弟UI。</param>
        /// <returns>节点有效、关系未改变且活跃或持有显示预约时返回true。</returns>
        private bool IsCurrentSibling(UIBase sibling)
        {
            // 销毁、普通失活和运行时换父都使原关系快照失效。
            if (!sibling || sibling == this ||
                // 销毁、自身或既失活又无预约的节点不能继续作为有效兄弟。
                (!sibling.gameObject.activeInHierarchy && !sibling.IsCurrentShowAction())) return false;
            // 读取当前直接父节点，按实时归属复核兄弟关系。
            var parent = transform.parent;
            // 有父节点时要求兄弟仍共享该直接父节点。
            if (parent) return sibling.transform.parent == parent;
            // 根界面要求兄弟仍为同一场景的根对象。
            return !sibling.transform.parent && sibling.gameObject.scene == gameObject.scene;
        }

        /// <summary>
        ///  判断快照节点当前是否仍是有效直接子UI。
        /// </summary>
        /// <param name="child">需要校验的子UI。</param>
        /// <returns>快照节点仍存活且直接父节点未改变时返回true。</returns>
        private bool IsCurrentChild(UIBase child)
        {
            // 入口资格已由FindChildren筛选；前序回调失活共同祖先不能丢弃尚未完成的隐藏。
            return child && child.transform.parent == transform;
        }

        /// <summary>
        ///  判断指定UI当前是否仍是可由父UI显示的直接子节点。
        /// </summary>
        /// <param name="child">需要校验的子UI。</param>
        /// <returns>节点有效且直接父节点未改变时返回true。</returns>
        private bool IsCurrentChildForParentShow(UIBase child)
        {
            // 隐藏行为可能主动失活GameObject，显示校验只约束对象存活和直接父子关系。
            return child && child.transform.parent == transform;
        }

        /// <summary>
        ///  判断指定UI当前是否仍位于连续祖先UI链中。
        /// </summary>
        /// <param name="parent">需要校验的父UI。</param>
        /// <returns>节点有效且仍位于当前连续UI祖先链中时返回true。</returns>
        private bool IsCurrentParent(UIBase parent)
        {
            // Unity对象可能在前一个生命周期回调中被销毁。
            if (!parent) return false;

            // 重新沿直接Transform链校验，遇到非UI节点即终止关系。
            var current = transform.parent;
            // 沿当前连续 UI 父链查找指定祖先。
            while (current)
            {
                // 普通 Transform 中断连续 UI 父链，原祖先关系失效。
                if (!current.TryGetComponent<UIBase>(out var currentParent)) return false;
                // 找到指定父界面后确认祖先关系仍然存在。
                if (currentParent == parent) return true;
                // 继续向更上一层父节点查找。
                current = current.parent;
            }

            // 完整连续父链中未找到目标祖先。
            return false;
        }
    }
}

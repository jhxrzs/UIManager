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
    public partial class UIBase : MonoBehaviour, IShowUI, IHideUI, IShowUIAsync, IHideUIAsync
    {
        /// <summary>
        ///  UI互斥组。
        /// </summary>
        [SerializeField, Tooltip("UI互斥组，同组UI无法同时显示")]
        private int mutexGroup;

        /// <summary>
        ///  是否固定UI层级。
        /// </summary>
        [SerializeField, Tooltip("固定UI层级")]
        private bool fixedSiblingIndex;

        /// <summary>
        ///  当前UI的状态。
        /// </summary>
        [SerializeField, Tooltip("当前UI状态")]
        private UIState state;

        /// <summary>
        ///  当前异步显隐操作的取消源。
        /// </summary>
        private CancellationTokenSource _currentShowHideCts;

        /// <summary>
        ///  当前显隐操作的递增版本，用于阻止重入后的旧操作继续提交状态。
        /// </summary>
        private int _showHideOperationVersion;

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
        private Func<CancellationToken, JTask> _onUIShowAsyncEvent;

        /// <summary>
        ///  当前的异步隐藏行为委托。
        /// </summary>
        private Func<CancellationToken, JTask> _onUIHideAsyncEvent;

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
        public event Func<CancellationToken, JTask> HideAsyncActions
        {
            add => _onUIHideAsyncEvent += value;
            remove => _onUIHideAsyncEvent -= value;
        }

        /// <summary>
        ///  增加UI异步显示行为的事件。
        /// </summary>
        public event Func<CancellationToken, JTask> ShowAsyncActions
        {
            add => _onUIShowAsyncEvent += value;
            remove => _onUIShowAsyncEvent -= value;
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
                state = value;
                OnStateChanged?.Invoke(previousState, value);
            }
        }

        /// <summary>
        ///  Unity首次启用组件时初始化序列化状态。
        /// </summary>
        protected virtual void Start()
        {
            // 根据序列化状态补齐对应的显隐行为。
            InitUIState();
        }

        /// <summary>
        ///  同步显示UI。
        /// </summary>
        public void Show()
        {
            // 已经显示时无需重复执行生命周期。
            if (State is UIState.Showed) return;
            ShowNotCheckState();
        }

        /// <summary>
        ///  同步隐藏UI。
        /// </summary>
        public void Hide()
        {
            // 已经隐藏时无需重复执行生命周期。
            if (State is UIState.Hided) return;
            HideNotCheckState();
        }

        /// <summary>
        ///  异步显示UI。
        /// </summary>
        /// <param name="cancellationToken">取消当前显示操作的令牌。</param>
        /// <returns>表示完整显示生命周期的异步任务。</returns>
        /// <exception cref="OperationCanceledException">操作取消或被更新请求取代时抛出。</exception>
        public async JTask ShowAsync(CancellationToken cancellationToken)
        {
            // 预取消必须在任何状态和事件副作用发生前被观察。
            ThrowIfShowHideCancellationRequested(cancellationToken);
            if (State is UIState.Showed) return;

            // 取消异常属于公开契约的一部分，不在此处吞掉。
            await ShowNotCheckStateAsync(cancellationToken);
        }

        /// <summary>
        ///  异步隐藏UI。
        /// </summary>
        /// <param name="cancellationToken">取消当前隐藏操作的令牌。</param>
        /// <returns>表示完整隐藏生命周期的异步任务。</returns>
        /// <exception cref="OperationCanceledException">操作取消或被更新请求取代时抛出。</exception>
        public async JTask HideAsync(CancellationToken cancellationToken)
        {
            // 预取消必须在任何状态和事件副作用发生前被观察。
            ThrowIfShowHideCancellationRequested(cancellationToken);
            if (State is UIState.Hided) return;

            // 取消异常属于公开契约的一部分，不在此处吞掉。
            await HideNotCheckStateAsync(cancellationToken);
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
        protected virtual async JTask OnUIShowAsync(CancellationToken cancellationToken)
        {
            // 显示前事件重入显隐时，不得再执行旧操作的同步或异步处理器。
            cancellationToken.ThrowIfCancellationRequested();

            // 没有异步显示委托时执行同步钩子，保持ShowAsync与Show的基础行为一致。
            if (_onUIShowAsyncEvent == null)
            {
                OnUIShow();
                return;
            }

            // 使用共享列表池保存本轮任务，避免每个UI长期持有任务集合。
            var taskList = ListPool<JTask>.Get();
            try
            {
                // GetInvocationList提供稳定的本轮委托快照。
                var invocationList = _onUIShowAsyncEvent.GetInvocationList();
                Exception invocationException = null;
                for (var i = 0; i < invocationList.Length; i++)
                {
                    // 取消后不再启动新行为，已启动行为仍由WhenAll完整观察。
                    if (cancellationToken.IsCancellationRequested) break;
                    var showAction = (Func<CancellationToken, JTask>)invocationList[i];
                    try
                    {
                        // 普通委托可能在返回JTask之前同步抛错。
                        taskList.Add(showAction.Invoke(cancellationToken));
                    }
                    catch (Exception exception)
                    {
                        // 停止启动后续行为，但仍需观察此前已经启动的任务。
                        invocationException = exception;
                        break;
                    }
                }

                // 保存组合任务异常，确保它能与同步委托异常一起报告。
                Exception taskException = null;
                try
                {
                    // 组合器不使用生命周期令牌提前结束等待。
                    await JTask.WhenAll(taskList);
                }
                catch (Exception exception)
                {
                    taskException = exception;
                }

                // 已启动任务全部结束后再传播一个或多个失败原因。
                ThrowHandlerExceptions(invocationException, taskException);
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                // 成功、取消或异常时都归还临时任务列表。
                ListPool<JTask>.Release(taskList);
            }
        }

        /// <summary>
        ///  并行执行全部异步隐藏行为。
        /// </summary>
        /// <param name="cancellationToken">传递给每个隐藏行为的操作令牌。</param>
        /// <returns>全部隐藏行为结束后的任务。</returns>
        protected virtual async JTask OnUIHideAsync(CancellationToken cancellationToken)
        {
            // 隐藏前事件重入显隐时，不得再执行旧操作的同步或异步处理器。
            cancellationToken.ThrowIfCancellationRequested();

            // 没有异步隐藏委托时执行同步钩子，保持HideAsync与Hide的基础行为一致。
            if (_onUIHideAsyncEvent == null)
            {
                OnUIHide();
                return;
            }

            // 使用共享列表池保存本轮任务。
            var taskList = ListPool<JTask>.Get();
            try
            {
                // GetInvocationList提供稳定的本轮委托快照。
                var invocationList = _onUIHideAsyncEvent.GetInvocationList();
                Exception invocationException = null;
                for (var i = 0; i < invocationList.Length; i++)
                {
                    // 取消后不再启动新行为，已启动行为仍由WhenAll完整观察。
                    if (cancellationToken.IsCancellationRequested) break;
                    var hideAction = (Func<CancellationToken, JTask>)invocationList[i];
                    try
                    {
                        // 普通委托可能在返回JTask之前同步抛错。
                        taskList.Add(hideAction.Invoke(cancellationToken));
                    }
                    catch (Exception exception)
                    {
                        // 停止启动后续行为，但仍需观察此前已经启动的任务。
                        invocationException = exception;
                        break;
                    }
                }

                // 保存组合任务异常，确保它能与同步委托异常一起报告。
                Exception taskException = null;
                try
                {
                    // 子任务自行传播异常，最终检查处理忽略令牌的行为。
                    await JTask.WhenAll(taskList);
                }
                catch (Exception exception)
                {
                    taskException = exception;
                }

                // 已启动任务全部结束后再传播一个或多个失败原因。
                ThrowHandlerExceptions(invocationException, taskException);
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                // 成功、取消或异常时都归还临时任务列表。
                ListPool<JTask>.Release(taskList);
            }
        }

        /// <summary>
        ///  执行不带状态短路的同步显示流程。
        /// </summary>
        private void ShowNotCheckState()
        {
            // 先登记新版本再取消旧异步操作。
            var operationVersion = BeginSynchronousShowHideAction();
            var siblings = ListPool<UIBase>.Get();
            var parents = ListPool<UIBase>.Get();
            try
            {
                // 取消回调重入后，旧流程不再继续。
                if (!IsCurrentShowHideAction(operationVersion)) return;
                if (!BeforeUIShowInvoke(siblings, parents, operationVersion)) return;

                // 执行当前面板自身的同步显示行为。
                OnUIShow();
                if (!IsCurrentShowHideAction(operationVersion)) return;

                // 使用同一份兄弟快照完成显示后流程。
                AfterUIShowedInvoke(siblings, operationVersion);
            }
            finally
            {
                // 临时关系列表不挂在UI实例上，避免常驻内存膨胀。
                ListPool<UIBase>.Release(parents);
                ListPool<UIBase>.Release(siblings);
            }
        }

        /// <summary>
        ///  执行不带状态短路的同步隐藏流程。
        /// </summary>
        private void HideNotCheckState()
        {
            // 先登记新版本再取消旧异步操作。
            var operationVersion = BeginSynchronousShowHideAction();
            var children = ListPool<UIBase>.Get();
            try
            {
                // 取消回调重入后，旧流程不再继续。
                if (!IsCurrentShowHideAction(operationVersion)) return;

                // 在生命周期开始时捕获直接子UI快照。
                FindChildren(children);
                if (!BeforeUIHideInvoke(children, operationVersion)) return;

                // 执行当前面板自身的同步隐藏行为。
                OnUIHide();
                if (!IsCurrentShowHideAction(operationVersion)) return;
                AfterUIHidedInvoke(operationVersion);
            }
            finally
            {
                // 归还本轮直接子UI快照。
                ListPool<UIBase>.Release(children);
            }
        }

        /// <summary>
        ///  执行不带状态短路的异步显示流程。
        /// </summary>
        /// <param name="cancellationToken">调用方提供的取消令牌。</param>
        /// <returns>本轮显示流程任务。</returns>
        private async JTask ShowNotCheckStateAsync(CancellationToken cancellationToken)
        {
            // 进入本轮操作前观察预取消和对象销毁。
            ThrowIfShowHideCancellationRequested(cancellationToken);

            // 捕获本轮局部取消源和版本，后续不重复读取可变字段。
            var operationCts = BeginAsynchronousShowHideAction(cancellationToken, out var operationVersion);
            var siblings = ListPool<UIBase>.Get();
            var parents = ListPool<UIBase>.Get();
            try
            {
                var operationToken = operationCts.Token;

                // 相邻UI联动与当前UI动画并行执行。
                var beforeTask = BeforeUIShowAsyncInvoke(siblings, parents, operationToken);
                operationToken.ThrowIfCancellationRequested();
                var showTask = OnUIShowAsync(operationToken);
                await JTask.WhenAll(beforeTask, showTask);

                // 即使处理器忽略取消，也只有最新操作可以提交状态。
                ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);
                AfterUIShowedInvoke(siblings, operationVersion, operationCts);

                // After回调也可能触发外部取消，成功完成前必须再观察一次。
                ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);
            }
            catch (Exception operationException)
            {
                // 失败时取消本轮派生操作，同时保留原始异常和取消回调异常。
                CancelFailedShowHideAction(operationCts, operationException);
                throw;
            }
            finally
            {
                try
                {
                    // 归还本轮祖先和兄弟快照。
                    ListPool<UIBase>.Release(parents);
                }
                finally
                {
                    // 即使第一个列表归还失败，也必须释放其余资源。
                    try
                    {
                        ListPool<UIBase>.Release(siblings);
                    }
                    finally
                    {
                        CompleteAsynchronousShowHideAction(operationCts);
                    }
                }
            }
        }

        /// <summary>
        ///  执行不带状态短路的异步隐藏流程。
        /// </summary>
        /// <param name="cancellationToken">调用方提供的取消令牌。</param>
        /// <returns>本轮隐藏流程任务。</returns>
        private async JTask HideNotCheckStateAsync(CancellationToken cancellationToken)
        {
            // 进入本轮操作前观察预取消和对象销毁。
            ThrowIfShowHideCancellationRequested(cancellationToken);

            // 在隐藏前事件发生前登记本轮操作，修复事件重入覆盖。
            var operationCts = BeginAsynchronousShowHideAction(cancellationToken, out var operationVersion);
            var children = ListPool<UIBase>.Get();
            try
            {
                // 捕获一次直接子UI关系并沿用到隐藏前流程结束。
                FindChildren(children);
                if (!BeforeUIHideInvoke(children, operationVersion, operationCts))
                    ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);

                // Before事件可能重入，执行动画前再次校验本轮身份。
                ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);
                await OnUIHideAsync(operationCts.Token);

                // 处理器结束后再次阻止过期操作提交状态。
                ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);
                AfterUIHidedInvoke(operationVersion, operationCts);

                // After回调也可能触发外部取消，成功完成前必须再观察一次。
                ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);
            }
            catch (Exception operationException)
            {
                // 失败时取消本轮派生操作，同时保留原始异常和取消回调异常。
                CancelFailedShowHideAction(operationCts, operationException);
                throw;
            }
            finally
            {
                // 成功或失败都必须归还子UI快照并释放链接令牌注册。
                try
                {
                    ListPool<UIBase>.Release(children);
                }
                finally
                {
                    CompleteAsynchronousShowHideAction(operationCts);
                }
            }
        }

        /// <summary>
        ///  执行UI隐藏前的关系联动与生命周期通知。
        /// </summary>
        /// <param name="children">本轮捕获的直接子UI快照。</param>
        /// <param name="operationVersion">本轮显隐操作版本。</param>
        /// <param name="operationCts">异步流程的本轮取消源；同步流程传null。</param>
        /// <returns>本轮操作仍为最新请求时返回true。</returns>
        private bool BeforeUIHideInvoke(List<UIBase> children, int operationVersion,
            CancellationTokenSource operationCts = null)
        {
            // 按快照顺序隐藏仍然属于当前UI的直接子节点。
            for (var i = 0; i < children.Count; i++)
            {
                var eachChild = children[i];
                if (!IsCurrentChild(eachChild)) continue;
                eachChild.Hide();

                // 子UI回调可能重入当前UI。
                if (!ValidateCurrentShowHideAction(operationVersion, operationCts)) return false;
            }

            // 先提交Hiding状态，再触发隐藏前事件。
            State = UIState.Hiding;
            if (!ValidateCurrentShowHideAction(operationVersion, operationCts)) return false;
            BeforeUIHideEvent?.Invoke();
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
            State = UIState.Hided;
            if (!ValidateCurrentShowHideAction(operationVersion, operationCts)) return;
            AfterUIHidedEvent?.Invoke();
        }

        /// <summary>
        ///  执行UI显示前的关系联动与生命周期通知。
        /// </summary>
        /// <param name="siblings">用于保存本轮兄弟UI快照的列表。</param>
        /// <param name="parents">用于保存本轮祖先UI快照的列表。</param>
        /// <param name="operationVersion">本轮显隐操作版本。</param>
        /// <returns>本轮操作仍为最新请求时返回true。</returns>
        private bool BeforeUIShowInvoke(List<UIBase> siblings, List<UIBase> parents, int operationVersion)
        {
            // 先提交Showing状态，确保后续回调观察到正确状态。
            State = UIState.Showing;
            if (!IsCurrentShowHideAction(operationVersion)) return false;

            // 先允许事件调整层级，再捕获本轮关系快照。
            BeforeUIShowEvent?.Invoke();
            if (!IsCurrentShowHideAction(operationVersion)) return false;
            FindSiblings(siblings);
            FindParents(parents);

            // 隐藏仍处于当前层级且属于同一互斥组的兄弟UI。
            for (var i = 0; i < siblings.Count; i++)
            {
                var eachSibling = siblings[i];
                if (!IsCurrentSibling(eachSibling) || eachSibling.mutexGroup != mutexGroup) continue;
                eachSibling.Hide();
                if (!IsCurrentShowHideAction(operationVersion)) return false;
            }

            // 从根祖先到直接父UI依次显示，避免并发重复启动同一祖先。
            for (var i = 0; i < parents.Count; i++)
            {
                var eachParent = parents[i];
                if (!IsCurrentParent(eachParent)) continue;
                eachParent.Show();
                if (!IsCurrentShowHideAction(operationVersion)) return false;
            }

            return IsCurrentShowHideAction(operationVersion);
        }

        /// <summary>
        ///  异步执行UI显示前的关系联动与生命周期通知。
        /// </summary>
        /// <param name="siblings">用于保存本轮兄弟UI快照的列表。</param>
        /// <param name="parents">用于保存本轮祖先UI快照的列表。</param>
        /// <param name="cancellationToken">本轮异步显隐操作令牌。</param>
        /// <returns>相邻UI联动全部结束后的任务。</returns>
        private async JTask BeforeUIShowAsyncInvoke(List<UIBase> siblings, List<UIBase> parents,
            CancellationToken cancellationToken)
        {
            // 状态和事件回调可能触发新显隐，因此每一步后检查取消。
            State = UIState.Showing;
            cancellationToken.ThrowIfCancellationRequested();
            BeforeUIShowEvent?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();

            // Transform层级是唯一关系数据源，本轮只捕获一次兄弟快照。
            FindSiblings(siblings);
            FindParents(parents);
            var taskList = ListPool<JTask>.Get();
            try
            {
                // 对同互斥组的当前兄弟UI发起异步隐藏。
                for (var i = 0; i < siblings.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    var eachSibling = siblings[i];
                    if (!IsCurrentSibling(eachSibling) || eachSibling.mutexGroup != mutexGroup) continue;
                    taskList.Add(eachSibling.HideAsync(cancellationToken));
                }

                // 先等待已启动的兄弟隐藏，异常时不留下无人观察的关系任务。
                await JTask.WhenAll(taskList);
                cancellationToken.ThrowIfCancellationRequested();

                // 从根祖先到直接父UI顺序等待，避免多个递归链并发取消同一祖先。
                for (var i = 0; i < parents.Count; i++)
                {
                    var eachParent = parents[i];
                    if (!IsCurrentParent(eachParent)) continue;
                    await eachParent.ShowAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            finally
            {
                // 关系任务只服务本轮操作。
                ListPool<JTask>.Release(taskList);
            }
        }

        /// <summary>
        ///  提交UI显示完成状态、调整兄弟层级并派发事件。
        /// </summary>
        /// <param name="siblings">本轮显示前捕获的兄弟UI快照。</param>
        /// <param name="operationVersion">本轮显隐操作版本。</param>
        /// <param name="operationCts">异步流程的本轮取消源；同步流程传null。</param>
        private void AfterUIShowedInvoke(List<UIBase> siblings, int operationVersion,
            CancellationTokenSource operationCts = null)
        {
            // 过期操作不得覆盖更新请求的状态。
            if (!ValidateCurrentShowHideAction(operationVersion, operationCts)) return;
            State = UIState.Showed;
            if (!ValidateCurrentShowHideAction(operationVersion, operationCts)) return;

            // 固定层级的UI不参与自动置顶。
            if (!fixedSiblingIndex)
            {
                var maxSiblingIndex = transform.GetSiblingIndex();
                var hasFlexibleSibling = false;
                for (var i = 0; i < siblings.Count; i++)
                {
                    var eachSibling = siblings[i];
                    if (!IsCurrentSibling(eachSibling) || eachSibling.fixedSiblingIndex) continue;
                    hasFlexibleSibling = true;
                    maxSiblingIndex = Mathf.Max(maxSiblingIndex, eachSibling.transform.GetSiblingIndex());
                }

                // 至少存在一个可变兄弟时才调整层级。
                if (hasFlexibleSibling) transform.SetSiblingIndex(maxSiblingIndex);
            }

            // 层级变化回调也可能重入显隐。
            if (!ValidateCurrentShowHideAction(operationVersion, operationCts)) return;
            AfterUIShowedEvent?.Invoke();
        }

        /// <summary>
        ///  开始一次同步显隐操作，并取消此前异步操作。
        /// </summary>
        /// <returns>本轮同步显隐操作版本。</returns>
        private int BeginSynchronousShowHideAction()
        {
            // 先摘除旧取消源，避免旧finally误清理后来创建的新操作。
            var previousCts = _currentShowHideCts;
            _currentShowHideCts = null;

            // 取消回调中的更新请求可以通过新版本取代本轮操作。
            var operationVersion = NextShowHideOperationVersion();
            previousCts?.Cancel();
            return operationVersion;
        }

        /// <summary>
        ///  开始一次异步显隐操作，并以局部取消源替换此前操作。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌。</param>
        /// <param name="operationVersion">返回本轮显隐操作版本。</param>
        /// <returns>仅属于本轮操作的链接取消源。</returns>
        private CancellationTokenSource BeginAsynchronousShowHideAction(CancellationToken cancellationToken,
            out int operationVersion)
        {
            // 任何可见副作用发生前观察取消。
            ThrowIfShowHideCancellationRequested(cancellationToken);
            var operationCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, destroyCancellationToken);

            // 先登记新操作，再取消旧操作，保证最后请求获胜。
            var previousCts = _currentShowHideCts;
            operationVersion = NextShowHideOperationVersion();
            _currentShowHideCts = operationCts;
            try
            {
                previousCts?.Cancel();
                ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);
                return operationCts;
            }
            catch (Exception)
            {
                // 启动失败时只清理由本方法登记的取消源。
                if (ReferenceEquals(_currentShowHideCts, operationCts)) _currentShowHideCts = null;
                operationCts.Dispose();
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
            if (ReferenceEquals(_currentShowHideCts, operationCts) &&
                IsCurrentShowHideAction(operationVersion)) return;

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
                _showHideOperationVersion++;
            }

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
            return true;
        }

        /// <summary>
        ///  在全部已启动处理器结束后传播同步调用异常和异步任务异常。
        /// </summary>
        /// <param name="invocationException">处理器返回JTask之前同步抛出的异常。</param>
        /// <param name="taskException">已启动处理器任务产生的异常。</param>
        /// <exception cref="AggregateException">同步和异步路径同时失败时抛出。</exception>
        private static void ThrowHandlerExceptions(Exception invocationException, Exception taskException)
        {
            // 两条路径都只表示取消时保持取消语义，不包装为故障聚合。
            if (invocationException is OperationCanceledException &&
                taskException is OperationCanceledException)
            {
                ExceptionDispatchInfo.Capture(invocationException).Throw();
                return;
            }

            // 两条路径同时失败且至少包含业务异常时保留全部原因。
            if (invocationException != null && taskException != null)
                throw new AggregateException(invocationException, taskException);

            // 单一异常使用ExceptionDispatchInfo保留原始调用栈。
            if (invocationException != null)
                ExceptionDispatchInfo.Capture(invocationException).Throw();
            if (taskException != null)
                ExceptionDispatchInfo.Capture(taskException).Throw();
        }

        /// <summary>
        ///  根据序列化状态初始化UI显隐。
        /// </summary>
        private void InitUIState()
        {
            // 已完成状态使用同步流程恢复实际显示效果。
            if (State == UIState.Showed) ShowNotCheckState();
            else if (State == UIState.Hided) HideNotCheckState();
            else if (State == UIState.Showing)
            {
                HideNotCheckState();
                _ = ShowAsync(destroyCancellationToken);
            }
            else if (State == UIState.Hiding)
            {
                ShowNotCheckState();
                _ = HideAsync(destroyCancellationToken);
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
            while (current)
            {
                if (!current.TryGetComponent<UIBase>(out var parentUI)) break;
                parents.Add(parentUI);
                current = current.parent;
            }

            // 显示时必须先保证根祖先完成，再处理更接近当前UI的父节点。
            parents.Reverse();
        }

        /// <summary>
        ///  将当前有效的兄弟UI写入指定快照列表。
        /// </summary>
        /// <param name="siblings">接收兄弟UI的可复用列表。</param>
        private void FindSiblings(List<UIBase> siblings)
        {
            // 池中列表写入前必须清空旧数据。
            siblings.Clear();
            var parent = transform.parent;
            if (parent)
            {
                // 非根节点只扫描父Transform的直接子节点。
                for (var i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    if (child == transform || !child.gameObject.activeInHierarchy) continue;
                    if (child.TryGetComponent<UIBase>(out var sibling)) siblings.Add(sibling);
                }

                return;
            }

            // 根节点关系限定在当前已加载Scene内。
            var scene = gameObject.scene;
            if (!scene.isLoaded) return;
            var rootObjects = ListPool<GameObject>.Get();
            try
            {
                scene.GetRootGameObjects(rootObjects);
                for (var i = 0; i < rootObjects.Count; i++)
                {
                    var rootObject = rootObjects[i];
                    if (rootObject == gameObject || !rootObject.activeInHierarchy) continue;
                    if (rootObject.TryGetComponent<UIBase>(out var sibling)) siblings.Add(sibling);
                }
            }
            finally
            {
                // 归还Scene根对象临时列表。
                ListPool<GameObject>.Release(rootObjects);
            }
        }

        /// <summary>
        ///  将当前有效的直接子UI写入指定快照列表。
        /// </summary>
        /// <param name="children">接收直接子UI的可复用列表。</param>
        private void FindChildren(List<UIBase> children)
        {
            // 只扫描直接子Transform，避免遍历整棵子树。
            children.Clear();
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                if (child.TryGetComponent<UIBase>(out var childUI)) children.Add(childUI);
            }
        }

        /// <summary>
        ///  判断快照节点当前是否仍是有效兄弟UI。
        /// </summary>
        /// <param name="sibling">需要校验的兄弟UI。</param>
        /// <returns>节点有效、活动且关系未改变时返回true。</returns>
        private bool IsCurrentSibling(UIBase sibling)
        {
            // 同时处理销毁、禁用GameObject和运行时换父。
            if (!sibling || sibling == this || !sibling.gameObject.activeInHierarchy) return false;
            var parent = transform.parent;
            if (parent) return sibling.transform.parent == parent;
            return !sibling.transform.parent && sibling.gameObject.scene == gameObject.scene;
        }

        /// <summary>
        ///  判断快照节点当前是否仍是有效直接子UI。
        /// </summary>
        /// <param name="child">需要校验的子UI。</param>
        /// <returns>节点有效、活动且直接父节点未改变时返回true。</returns>
        private bool IsCurrentChild(UIBase child)
        {
            // Unity对象可能在前一个回调中被销毁或换父。
            return child && child.gameObject.activeInHierarchy && child.transform.parent == transform;
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
            while (current)
            {
                if (!current.TryGetComponent<UIBase>(out var currentParent)) return false;
                if (currentParent == parent) return true;
                current = current.parent;
            }

            return false;
        }
    }
}

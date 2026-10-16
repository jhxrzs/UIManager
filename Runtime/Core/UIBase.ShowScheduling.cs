using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace JH
{
    /// <summary>
    ///  UIBase父链准入与同组兄弟显示优先级调度实现。
    /// </summary>
    public partial class UIBase
    {
        // 只有自动补父链阶段完成后，后续复核才要求当前父链持续就绪。
        private int _readyAutoParentChainVersion = -1;

        /// <summary>
        ///  判断指定来源的显示请求当前是否具备父链和兄弟互斥组准入条件。
        /// </summary>
        /// <param name="showMode">当前显示请求的来源模式。</param>
        /// <param name="parentCascadeSource">父级联请求的直接发起父UI；普通显示传null。</param>
        /// <param name="parentCascadeSourceOperationVersion">来源父UI显示事务版本；普通显示传-1。</param>
        /// <param name="isRevalidation">是否为已开始显示操作的准入复核，用于区分诊断信息。</param>
        /// <param name="requireAutoParentsReady">自动补父链已完成，此后父链必须持续就绪。</param>
        /// <returns>显示请求可以进入状态和生命周期流程时返回true。</returns>
        private bool CanScheduleShow(UIShowMode showMode, UIBase parentCascadeSource = null,
            int parentCascadeSourceOperationVersion = -1, bool isRevalidation = false,
            bool requireAutoParentsReady = false, bool allowScheduledParents = false)
        {
            // 父级联豁免只在当前UI仍直属来源父UI且来源显示事务仍有效时成立。
            if (showMode is UIShowMode.ParentCascade)
            {
                if (!parentCascadeSource)
                {
                    LogShowRejection(ShowRejectionReason.CascadeSourceUnavailable, showMode, isRevalidation);
                    return false;
                }
                if (transform.parent != parentCascadeSource.transform)
                {
                    LogShowRejection(ShowRejectionReason.CascadeParentChanged, showMode, isRevalidation,
                        parentCascadeSource);
                    return false;
                }
                if (!parentCascadeSource.IsCurrentShowHideAction(parentCascadeSourceOperationVersion))
                {
                    LogShowRejection(ShowRejectionReason.CascadeOperationReplaced, showMode, isRevalidation,
                        parentCascadeSource);
                    return false;
                }
            }

            // 父UI主动级联属于同一父级显示事务，不重复要求父级已经提交Showed。
            if (showMode is UIShowMode.Normal &&
                parentShowPolicy is UIParentShowPolicy.RequireParentChainShowedForDirectShow &&
                !IsContinuousParentChainShowed(true, out var blockingParent, allowScheduledParents))
            {
                LogShowRejection(blockingParent ? ShowRejectionReason.RequiredParentNotReady :
                    ShowRejectionReason.MissingRequiredParent, showMode, isRevalidation, blockingParent);
                return false;
            }

            if (requireAutoParentsReady && ShouldAutoShowParentChain(showMode) &&
                !IsContinuousParentChainShowed(false, out var unavailableAutoParent, true))
            {
                LogShowRejection(ShowRejectionReason.AutoParentNotReady, showMode, isRevalidation,
                    unavailableAutoParent);
                return false;
            }

            // 更高优先级只在同父同组且仍持有有效显示权的兄弟之间阻止当前请求。
            if (HasHigherPrioritySiblingHoldingMutex(out var blockingSibling))
            {
                LogShowRejection(ShowRejectionReason.HigherPrioritySibling, showMode, isRevalidation, blockingSibling);
                return false;
            }
            return true;
        }

        private bool IsContinuousParentChainShowed(bool requireDirectParent, out UIBase blockingParent,
            bool allowScheduledParents = false)
        {
            blockingParent = null;
            // 当前Transform没有直接父节点时无法满足显式父UI准入约束。
            var currentParent = transform.parent;
            if (!currentParent) return !requireDirectParent;

            // 直接父Transform必须挂载UIBase，否则当前层级不属于连续父UI链。
            if (!currentParent.TryGetComponent<UIBase>(out var parentUI)) return !requireDirectParent;

            // 沿现有UIBase关系规则逐级检查，遇到首个非UI父节点后结束连续父链。
            while (parentUI)
            {
                // 序列化Showed但尚未执行实际生命周期的父UI不能作为已就绪页面。
                if (!parentUI.IsShowRelationReady(allowScheduledParents))
                {
                    blockingParent = parentUI;
                    return false;
                }

                // 移动到当前父UI的上一级Transform，准备继续检查祖先UI。
                currentParent = currentParent.parent;
                if (!currentParent) break;

                // 非UI容器会终止连续父UI链，其更上层对象不属于当前UIBase关系。
                if (!currentParent.TryGetComponent(out parentUI)) break;
            }

            // 直接父UI存在且已通过整条连续链检查时允许本次普通显示请求。
            return true;
        }

        /// <summary>
        ///  判断严格序列化Showed初态是否需要等待启用中的父UI执行Start。
        /// </summary>
        /// <returns>连续父UI链中存在已启用但尚未初始化的UI时返回true。</returns>
        private bool ShouldDeferStrictInitialShow()
        {
            // 只有严格Showed初态会因父子Start顺序产生不确定结果。
            if (parentShowPolicy is not UIParentShowPolicy.RequireParentChainShowedForDirectShow ||
                State is not UIState.Showed) return false;

            // 沿连续UIBase父链同时查找待初始化节点和已经确定的失败条件。
            var hasPendingParent = false;
            var currentParent = transform.parent;
            while (currentParent && currentParent.TryGetComponent<UIBase>(out var parentUI))
            {
                // 已初始化父UI只要未激活或未Showed，整条父链就已确定无法通过准入。
                if (parentUI._isUIStateInitialized)
                {
                    if (!parentUI.gameObject.activeInHierarchy || parentUI.State is not UIState.Showed)
                        return false;
                    currentParent = currentParent.parent;
                    continue;
                }

                // 未启用父UI不会自动执行Start，不能为了它延后当前子UI初始化。
                if (!parentUI.isActiveAndEnabled) return false;

                // 当前父UI仍可能在本帧后续Start或自己的延后初始化中完成Showed。
                hasPendingParent = true;
                currentParent = currentParent.parent;
            }

            // 只有不存在确定失败且至少一个启用父UI尚待初始化时才需要延后。
            return hasPendingParent;
        }

        /// <summary>
        ///  获取当前连续父UI链的节点数量，作为严格初态延后初始化的最大等待帧数。
        /// </summary>
        /// <returns>从直接父UI到连续链最上层父UI的节点数量。</returns>
        private int GetContinuousParentUIChainLength()
        {
            // 普通Transform会终止UIBase关系，等待预算不跨越非UI容器。
            var parentCount = 0;
            var currentParent = transform.parent;
            while (currentParent && currentParent.TryGetComponent<UIBase>(out _))
            {
                // 每层延后父UI在最坏的子先恢复顺序下最多额外占用一帧。
                parentCount++;
                currentParent = currentParent.parent;
            }

            // 没有直接父UI时返回0，调用方会立即按严格准入规则规范化初态。
            return parentCount;
        }

        /// <summary>
        ///  启动或恢复严格Showed序列化初态的有限帧延后初始化。
        /// </summary>
        private void DeferStrictInitialShow()
        {
            // 标记必须先于启动协程提交，GameObject失活时OnEnable据此恢复流程。
            _isStrictInitialShowDeferred = true;
            int deferredVersion;
            unchecked
            {
                // 新协程使用更新版本，使组件禁用重启等情况下的旧协程无法重复初始化。
                deferredVersion = ++_strictInitialShowDeferredVersion;
            }

            // Unity协程由当前GameObject托管，重新启用时会通过OnEnable创建更新版本。
            StartCoroutine(InitUIStateAfterParentsInitialized(deferredVersion));
        }

        /// <summary>
        ///  在有限帧内等待启用中的连续父UI完成Start，再初始化严格Showed子UI。
        /// </summary>
        /// <param name="deferredVersion">本轮延后初始化协程版本。</param>
        /// <returns>逐帧等待父UI初始化并恢复当前UI序列化初态的协程。</returns>
        private IEnumerator InitUIStateAfterParentsInitialized(int deferredVersion)
        {
            // 等待预算按连续父链深度限定，派生类遗漏base.Start时不会留下永久轮询任务。
            var remainingWaitFrames = GetContinuousParentUIChainLength();
            while (remainingWaitFrames > 0 && ShouldDeferStrictInitialShow())
            {
                // 每次让出一帧后递减预算，并在下一轮按最新状态和Transform关系复核。
                remainingWaitFrames--;
                yield return null;

                // 更新协程或公开显隐已经接管初态时，旧协程不得继续派发生命周期。
                if (deferredVersion != _strictInitialShowDeferredVersion || _isUIStateInitialized)
                    yield break;
            }

            // 只有仍为最新版本的协程可以结束延后标记并提交初始化。
            if (deferredVersion != _strictInitialShowDeferredVersion || _isUIStateInitialized)
                yield break;
            _isStrictInitialShowDeferred = false;

            // 恢复初始化同样经过调度器，执行前复核是否已被其他请求接管。
            UIOperationDispatcher.Run(InitializeUIState);
        }

        /// <summary>
        ///  判断当前UI是否存在同父、同互斥组且仍持有更高显示权的兄弟UI。
        /// </summary>
        /// <returns>存在更高优先级兄弟仍持有有效显示权或预约时返回true。</returns>
        private bool HasHigherPrioritySiblingHoldingMutex(out UIBase blockingSibling)
        {
            blockingSibling = null;
            // 使用独立的调度快照，使入口预约即使尚未激活GameObject也能参与仲裁。
            var siblings = ListPool<UIBase>.Get();
            try
            {
                // 每次调度均捕获当前有效兄弟快照，不在实例上缓存易失层级关系。
                FindSiblingsForShowScheduling(siblings);
                for (var i = 0; i < siblings.Count; i++)
                {
                    // 只比较仍在当前层级且属于相同互斥组的兄弟UI。
                    var sibling = siblings[i];
                    if (!IsCurrentSiblingForShowScheduling(sibling) ||
                        sibling.mutexGroup != mutexGroup) continue;

                    // 相等优先级继续保留后请求者获胜，较低优先级不会阻止当前请求。
                    if (sibling.mutexPriority <= mutexPriority) continue;

                    // 只有已经显示、执行有效隐藏或持有有效显示预约的兄弟才占用优先级。
                    if (sibling.IsHoldingMutexPriority())
                    {
                        blockingSibling = sibling;
                        return true;
                    }
                }

                // 当前没有更高优先级兄弟持有同一互斥组显示权。
                return false;
            }
            finally
            {
                // 正常完成和提前发现阻止项时都归还临时兄弟快照。
                ListPool<UIBase>.Release(siblings);
            }
        }

        /// <summary>
        ///  判断当前UI是否正在持有其互斥组显示优先级。
        /// </summary>
        /// <returns>存在有效显示预约、有效隐藏操作或激活的Showed状态时返回true。</returns>
        private bool IsHoldingMutexPriority()
        {
            // 已经登记但尚未进入Showing的显示请求同样必须参与同帧和重入仲裁。
            if (IsCurrentShowAction()) return true;

            // 未执行过实际显隐生命周期的序列化状态不能提前占用互斥组优先级。
            if (!_isUIStateInitialized) return false;

            // 只有真正执行中的Hiding才继续占权，取消或故障残留状态交由后续请求恢复。
            if (State is UIState.Hiding) return IsCurrentHideAction();

            // 已失活GameObject不再以残留Showed状态占用互斥组。
            return State is UIState.Showed && gameObject.activeInHierarchy;
        }

        /// <summary>
        ///  将同一直接父节点下可能持有显示优先级的兄弟UI写入快照。
        /// </summary>
        /// <param name="siblings">接收调度兄弟UI的可复用列表。</param>
        private void FindSiblingsForShowScheduling(List<UIBase> siblings)
        {
            // 池中列表写入前必须清空旧引用。
            siblings.Clear();
            var parent = transform.parent;
            if (parent)
            {
                // 调度快照不过滤失活对象，具体是否占权由IsHoldingMutexPriority判定。
                for (var i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    if (child == transform) continue;
                    if (child.TryGetComponent<UIBase>(out var sibling)) siblings.Add(sibling);
                }

                return;
            }

            // 根节点仅与同一已加载Scene的其他根UI互为兄弟。
            var scene = gameObject.scene;
            if (!scene.isLoaded) return;
            var rootObjects = ListPool<GameObject>.Get();
            try
            {
                // 遍历Scene根对象时同样保留失活但可能有预约的UI。
                scene.GetRootGameObjects(rootObjects);
                for (var i = 0; i < rootObjects.Count; i++)
                {
                    var rootObject = rootObjects[i];
                    if (rootObject == gameObject) continue;
                    if (rootObject.TryGetComponent<UIBase>(out var sibling)) siblings.Add(sibling);
                }
            }
            finally
            {
                // 正常完成和异常时都归还Scene根对象列表。
                ListPool<GameObject>.Release(rootObjects);
            }
        }

        /// <summary>
        ///  判断指定UI是否仍是当前UI的同父调度兄弟。
        /// </summary>
        /// <param name="sibling">需要校验的兄弟UI。</param>
        /// <returns>对象有效且直接父关系未变化时返回true。</returns>
        private bool IsCurrentSiblingForShowScheduling(UIBase sibling)
        {
            // 调度关系仅校验对象和层级，失活状态由占权判定单独处理。
            if (!sibling || sibling == this) return false;
            var parent = transform.parent;
            if (parent) return sibling.transform.parent == parent;
            return !sibling.transform.parent && sibling.gameObject.scene == gameObject.scene;
        }

        /// <summary>
        ///  取消尚未离开Hided状态的当前显示预约。
        /// </summary>
        private void CancelHidedShowReservation()
        {
            // 新同步版本会使显示预约失效并取消其异步令牌，但不派发隐藏生命周期。
            BeginSynchronousShowHideAction();
        }

        /// <summary>
        ///  判断当前显示来源是否需要由UIBase自动显示连续父UI链。
        /// </summary>
        /// <param name="showMode">当前显示请求的来源模式。</param>
        /// <returns>普通显示且配置为自动补齐父链时返回true。</returns>
        private bool ShouldAutoShowParentChain(UIShowMode showMode)
        {
            // 父级联不再向上递归，严格父链策略也禁止当前UI主动拉起父页面。
            return showMode is UIShowMode.Normal &&
                   parentShowPolicy is not UIParentShowPolicy.RequireParentChainShowedForDirectShow;
        }

        /// <summary>
        ///  在显示生命周期已经开始后重新校验准入，失去资格时以隐藏请求安全取代当前显示。
        /// </summary>
        /// <param name="showMode">当前显示请求的来源模式。</param>
        /// <param name="operationVersion">当前显示操作版本。</param>
        /// <param name="operationCts">异步显示操作的取消源；同步显示传null。</param>
        /// <returns>当前显示仍可继续时返回true；同步显示被隐藏取代时返回false。</returns>
        private bool MaintainCurrentShowAdmission(UIShowMode showMode, int operationVersion,
            CancellationTokenSource operationCts = null)
        {
            // 先阻止已经被其他重入请求取代的旧流程继续读取关系或改变状态。
            if (!ValidateCurrentShowHideAction(operationVersion, operationCts)) return false;

            // 父级联只能沿登记时的直接父关系继续，普通显示不携带来源父UI。
            var parentCascadeSource = showMode is UIShowMode.ParentCascade &&
                                      _showModeOperationVersion == operationVersion
                ? _currentParentCascadeSource
                : null;
            var parentCascadeSourceOperationVersion = showMode is UIShowMode.ParentCascade &&
                                                      _showModeOperationVersion == operationVersion
                ? _currentParentCascadeSourceOperationVersion
                : -1;

            // 父链、级联来源和同组优先级仍满足时保留当前显示操作。
            if (CanScheduleShow(
                    showMode,
                    parentCascadeSource,
                    parentCascadeSourceOperationVersion,
                    isRevalidation: true,
                    requireAutoParentsReady: _readyAutoParentChainVersion == operationVersion,
                    allowScheduledParents: _allowScheduledParentsVersion == operationVersion)) return true;

            return StopCurrentShow(operationVersion, operationCts);
        }

        /// <summary>父调用结束后确认实际结果；仅等待任务结束不能证明父已成功显示。</summary>
        private bool MaintainAutoParentResult(UIBase parent, int operationVersion,
            CancellationTokenSource operationCts = null)
        {
            if (!ValidateCurrentShowHideAction(operationVersion, operationCts)) return false;
            // 回调已改变层级时不再要求旧父，最新连续父链由阶段结束后的统一复核处理。
            if (!IsCurrentParent(parent)) return true;
            if (parent.IsShowRelationReady(true))
                return true;

            LogShowRejection(ShowRejectionReason.AutoParentNotReady, UIShowMode.Normal, true, parent);
            return StopCurrentShow(operationVersion, operationCts);
        }

        private bool StopCurrentShow(int operationVersion, CancellationTokenSource operationCts)
        {
            // 新隐藏请求会递增版本并取消当前异步令牌，确保已启动显示行为能够完整收口。
            ExecuteHide();

            // 异步路径必须把更新取消传播给原显示调用方，不能把已开始的显示误报为成功。
            if (operationCts != null)
                ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);

            // 同步显示已经由新版本隐藏流程取代，旧调用栈只需停止继续执行。
            return false;
        }

        /// <summary>动画恢复只核验既有决策；新层级仍有可见竞争者时停止自己，不再调度隐藏别人。</summary>
        private void MaintainCommittedShowRelations(List<UIBase> siblings, UIShowMode showMode,
            int operationVersion, CancellationTokenSource operationCts)
        {
            MaintainCurrentShowAdmission(showMode, operationVersion, operationCts);
            FindSiblings(siblings);
            foreach (var sibling in siblings)
            {
                if (!IsCurrentSibling(sibling) || sibling.mutexGroup != mutexGroup ||
                    !RequiresSiblingHide(sibling)) continue;
                if (sibling.State == UIState.Hiding && sibling.IsCurrentHideAction()) continue;
                LogShowRejection(ShowRejectionReason.CommittedMutexChanged, showMode, true, sibling);
                StopCurrentShow(operationVersion, operationCts);
            }
        }

        /// <summary>
        ///  按当前动态层级同步隐藏全部同组兄弟，直到本轮显示取得稳定互斥权。
        /// </summary>
        /// <param name="siblings">用于重复捕获当前兄弟关系的快照列表。</param>
        /// <param name="showMode">当前显示请求的来源模式。</param>
        /// <param name="operationVersion">当前显示操作版本。</param>
        /// <returns>所有当前同组兄弟已隐藏且本轮显示仍有效时返回true。</returns>
        private bool ReconcileCurrentShowSiblings(List<UIBase> siblings, UIShowMode showMode,
            int operationVersion)
        {
            // 生命周期回调可能动态换父，每隐藏一个竞争项后都重新捕获关系。
            while (true)
            {
                // 先完整预检更高优先级，保证被拒绝时不会先部分隐藏其他兄弟。
                if (!MaintainCurrentShowAdmission(showMode, operationVersion)) return false;
                FindSiblings(siblings);

                // 每轮只选取一个当前竞争项，隐藏回调改变层级后不继续使用旧快照。
                UIBase siblingToHide = null;
                for (var i = 0; i < siblings.Count; i++)
                {
                    var eachSibling = siblings[i];
                    if (!IsCurrentSibling(eachSibling) ||
                        eachSibling.mutexGroup != mutexGroup ||
                        !RequiresSiblingHide(eachSibling)) continue;
                    siblingToHide = eachSibling;
                    break;
                }

                // 没有剩余竞争项时再次复核准入，并保留本轮最新兄弟快照供置顶使用。
                if (!siblingToHide)
                    return MaintainCurrentShowAdmission(showMode, operationVersion);

                // 同组低优先级和相等优先级均由当前后请求完整隐藏。
                siblingToHide.ExecuteHide();
            }
        }

        /// <summary>
        ///  按当前动态层级异步隐藏全部同组兄弟，直到本轮显示取得稳定互斥权。
        /// </summary>
        /// <param name="siblings">用于重复捕获当前兄弟关系的快照列表。</param>
        /// <param name="showMode">当前显示请求的来源模式。</param>
        /// <param name="operationVersion">当前显示操作版本。</param>
        /// <param name="operationCts">当前异步显示操作的取消源。</param>
        /// <returns>所有当前同组兄弟异步隐藏完成后的任务。</returns>
        private async UniTask ReconcileCurrentShowSiblingsAsync(List<UIBase> siblings, UIShowMode showMode,
            int operationVersion, CancellationTokenSource operationCts)
        {
            var tasks = ListPool<UniTask>.Get();
            try
            {
                Exception schedulingException = null;
                try
                {
                    // 同步完成关系调度；每次隐藏后的同步回调仍可能换父，因此重新取快照。
                    while (true)
                    {
                        MaintainCurrentShowAdmission(showMode, operationVersion, operationCts);
                        FindSiblings(siblings);
                        UIBase toHide = null;
                        foreach (var sibling in siblings)
                        {
                            if (!IsCurrentSibling(sibling) || sibling.mutexGroup != mutexGroup ||
                                !RequiresSiblingHide(sibling)) continue;
                            // 隐藏目标已经提交，动画尚未结束不需要重复隐藏或重启动画。
                            if (sibling.State == UIState.Hiding && sibling.IsCurrentHideAction()) continue;
                            toHide = sibling;
                            break;
                        }
                        if (!toHide) break;
                        tasks.Add(toHide.ExecuteHideAsync(operationCts.Token));
                        // 同步抛错后留下的 Hiding 不可在这一轮被无限重试。
                        if (toHide && toHide.State != UIState.Hided && !toHide.IsCurrentHideAction()) break;
                    }
                }
                catch (Exception exception) { schedulingException = exception; }
                Exception presentationException = null;
                try { await UIOperationDispatcher.Continue(UIAsync.WaitAll(tasks)); }
                catch (Exception exception) { presentationException = exception; }
                ThrowHandlerExceptions(schedulingException, presentationException);
            }
            finally { ListPool<UniTask>.Release(tasks); }
        }

        /// <summary>
        ///  判断指定同组兄弟是否需要由当前显示请求收口隐藏。
        /// </summary>
        /// <param name="sibling">需要检查的兄弟UI。</param>
        /// <returns>兄弟尚未Hided或仍持有显示预约时返回true。</returns>
        private static bool RequiresSiblingHide(UIBase sibling)
        {
            // Showing、Showed及执行中或残留Hiding都应收口，Hided入口预约则需单独取消。
            return sibling && (sibling.State is not UIState.Hided || sibling.IsCurrentShowAction());
        }
    }
}

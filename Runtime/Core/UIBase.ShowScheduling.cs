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
        /// <summary>已完成自动父链关系调度并允许进入后续阶段的操作版本。</summary>
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
        /// <param name="allowScheduledParents">是否允许复用已完成关系调度但动画尚未结束的祖先。</param>
        private bool CanScheduleShow(UIShowMode showMode, UIBase parentCascadeSource = null,
            int parentCascadeSourceOperationVersion = -1, bool isRevalidation = false,
            bool requireAutoParentsReady = false, bool allowScheduledParents = false)
        {
            // 父级联豁免只在当前UI仍直属来源父UI且来源显示事务仍有效时成立。
            if (showMode is UIShowMode.ParentCascade)
            {
                // 父级联必须仍有存活的来源界面。
                if (!parentCascadeSource)
                {
                    // 记录父级联来源不存在的拒绝原因。
                    LogShowRejection(ShowRejectionReason.CascadeSourceUnavailable, showMode, isRevalidation);
                    // 来源丢失后不允许当前级联继续显示。
                    return false;
                }
                // 当前直属父节点必须仍与原级联来源一致。
                if (transform.parent != parentCascadeSource.transform)
                {
                    // 记录运行中换父导致的父级联失效。
                    LogShowRejection(ShowRejectionReason.CascadeParentChanged, showMode, isRevalidation,
                        parentCascadeSource);
                    // 当前关系已不属于原父级联，拒绝继续显示。
                    return false;
                }
                // 来源父界面的显示事务必须仍是原版本。
                if (!parentCascadeSource.IsCurrentShowHideAction(parentCascadeSourceOperationVersion))
                {
                    // 记录父级联来源操作被替代的拒绝原因。
                    LogShowRejection(ShowRejectionReason.CascadeOperationReplaced, showMode, isRevalidation,
                        parentCascadeSource);
                    // 过期的父级联操作不能继续显示子界面。
                    return false;
                }
            }

            // 父UI主动级联属于同一父级显示事务，不重复要求父级已经提交Showed。
            if (showMode is UIShowMode.Normal &&
                parentShowPolicy is UIParentShowPolicy.RequireParentChainShowedForDirectShow &&
                !IsContinuousParentChainShowed(true, out var blockingParent, allowScheduledParents))
            {
                // 根据阻挡祖先是否存在区分父链未就绪和缺少直接父界面。
                LogShowRejection(blockingParent ? ShowRejectionReason.RequiredParentNotReady :
                    ShowRejectionReason.MissingRequiredParent, showMode, isRevalidation, blockingParent);
                // 严格父链条件不满足时拒绝当前显示。
                return false;
            }

            // 要求自动父链就绪时，检查当前连续祖先是否达到所需显示阶段。
            if (requireAutoParentsReady && ShouldAutoShowParentChain(showMode) &&
                !IsContinuousParentChainShowed(false, out var unavailableAutoParent, true))
            {
                // 记录自动补父链后仍未就绪的关联祖先。
                LogShowRejection(ShowRejectionReason.AutoParentNotReady, showMode, isRevalidation,
                    unavailableAutoParent);
                // 自动父链未就绪时停止目标显示。
                return false;
            }

            // 更高优先级只在同父同组且仍持有有效显示权的兄弟之间阻止当前请求。
            if (HasHigherPrioritySiblingHoldingMutex(out var blockingSibling))
            {
                // 记录当前同父同组更高优先级持有者。
                LogShowRejection(ShowRejectionReason.HigherPrioritySibling, showMode, isRevalidation, blockingSibling);
                // 更高优先级兄弟仍持有显示权时拒绝目标。
                return false;
            }
            // 父链与互斥准入检查均通过。
            return true;
        }

        /// <summary>检查当前连续父界面链是否满足要求，并返回首个阻挡祖先。</summary>
        /// <param name="requireDirectParent">是否要求目标存在挂载 UIBase 的直接父节点。</param>
        /// <param name="blockingParent">返回第一个尚未满足要求的父界面，没有时为空。</param>
        /// <param name="allowScheduledParents">是否允许复用已完成关系调度但动画尚未结束的祖先。</param>
        /// <returns>连续父链满足要求且不存在阻挡祖先时为真。</returns>
        private bool IsContinuousParentChainShowed(bool requireDirectParent, out UIBase blockingParent,
            bool allowScheduledParents = false)
        {
            // 默认没有阻挡祖先，发现失败节点时再赋值。
            blockingParent = null;
            // 当前Transform没有直接父节点时无法满足显式父UI准入约束。
            var currentParent = transform.parent;
            // 根节点仅在策略不强制直接父界面时允许通过。
            if (!currentParent) return !requireDirectParent;

            // 直接父Transform必须挂载UIBase，否则当前层级不属于连续父UI链。
            if (!currentParent.TryGetComponent<UIBase>(out var parentUI)) return !requireDirectParent;

            // 沿现有UIBase关系规则逐级检查，遇到首个非UI父节点后结束连续父链。
            while (parentUI)
            {
                // 序列化Showed但尚未执行实际生命周期的父UI不能作为已就绪页面。
                if (!parentUI.IsShowRelationReady(allowScheduledParents))
                {
                    // 返回第一个未满足显示关系要求的祖先。
                    blockingParent = parentUI;
                    // 连续父链存在未就绪节点，拒绝当前显示。
                    return false;
                }

                // 移动到当前父UI的上一级Transform，准备继续检查祖先UI。
                currentParent = currentParent.parent;
                // 到达层级根后结束连续父链扫描。
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
                // 非严格初态或没有可用直接父界面时不进入延后机制。
                State is not UIState.Showed) return false;

            // 沿连续UIBase父链同时查找待初始化节点和已经确定的失败条件。
            var hasPendingParent = false;
            // 从直接父节点开始判断是否仅需等待祖先初始化。
            var currentParent = transform.parent;
            // 只检查连续 UI 父链，普通 Transform 截断等待范围。
            while (currentParent && currentParent.TryGetComponent<UIBase>(out var parentUI))
            {
                // 已初始化父UI只要未激活或未Showed，整条父链就已确定无法通过准入。
                if (parentUI._isUIStateInitialized)
                {
                    // 已初始化但不处于活动显示状态的祖先不能靠等待 Start 修复。
                    if (!parentUI.gameObject.activeInHierarchy || parentUI.State is not UIState.Showed)
                        // 该祖先需要真实显示请求，不继续延后初态等待。
                        return false;
                    // 当前祖先就绪，继续检查更上一级。
                    currentParent = currentParent.parent;
                    // 跳过本轮后续未初始化祖先的特殊检查。
                    continue;
                }

                // 未启用父UI不会自动执行Start，不能为了它延后当前子UI初始化。
                if (!parentUI.isActiveAndEnabled) return false;

                // 当前父UI仍可能在本帧后续Start或自己的延后初始化中完成Showed。
                hasPendingParent = true;
                // 检查更上一层尚未完成初始化的父界面。
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
            // 从直接父 Transform 开始计算连续 UI 深度。
            var currentParent = transform.parent;
            // 只有连续挂载 UIBase 的祖先计入等待预算。
            while (currentParent && currentParent.TryGetComponent<UIBase>(out _))
            {
                // 每层延后父UI在最坏的子先恢复顺序下最多额外占用一帧。
                parentCount++;
                // 继续向更高层父节点累计链长度。
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
            // 保存本次延后流程对应的版本号。
            int deferredVersion;
            // 允许版本自然回绕，避免整数溢出异常打断初态恢复。
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
            // 仅在仍需等待且未耗尽父链帧预算时继续挂起。
            while (remainingWaitFrames > 0 && ShouldDeferStrictInitialShow())
            {
                // 每次让出一帧后递减预算，并在下一轮按最新状态和Transform关系复核。
                remainingWaitFrames--;
                // 让祖先有机会在下一帧执行初始化。
                yield return null;

                // 更新协程或公开显隐已经接管初态时，旧协程不得继续派发生命周期。
                if (deferredVersion != _strictInitialShowDeferredVersion || _isUIStateInitialized)
                    // 延后流程被新版本取代后立即结束旧协程。
                    yield break;
            }

            // 只有仍为最新版本的协程可以结束延后标记并提交初始化。
            if (deferredVersion != _strictInitialShowDeferredVersion || _isUIStateInitialized)
                // 对象失效或已被其他真实操作初始化时停止等待。
                yield break;
            // 退出延后状态，交给正常初态入口重新判断准入。
            _isStrictInitialShowDeferred = false;

            // 恢复初始化同样经过调度器，执行前复核是否已被其他请求接管。
            UIOperationDispatcher.Run(InitializeUIState);
        }

        /// <summary>
        ///  判断当前UI是否存在同父、同互斥组且仍持有更高显示权的兄弟UI。
        /// </summary>
        /// <returns>存在更高优先级兄弟仍持有有效显示权或预约时返回true。</returns>
        /// <param name="blockingSibling">返回仍持有更高互斥优先级的兄弟界面，没有时为空。</param>
        private bool HasHigherPrioritySiblingHoldingMutex(out UIBase blockingSibling)
        {
            // 默认不存在更高优先级阻挡兄弟。
            blockingSibling = null;
            // 使用独立的调度快照，使入口预约即使尚未激活GameObject也能参与仲裁。
            var siblings = ListPool<UIBase>.Get();
            // 无论是否提前找到阻挡者，都归还兄弟快照。
            try
            {
                // 每次调度均捕获当前有效兄弟快照，不在实例上缓存易失层级关系。
                FindSiblingsForShowScheduling(siblings);
                // 按快照检查当前同父兄弟的互斥资格。
                for (var i = 0; i < siblings.Count; i++)
                {
                    // 只比较仍在当前层级且属于相同互斥组的兄弟UI。
                    var sibling = siblings[i];
                    // 跳过已换父、不同互斥组或不持有更高优先级的节点。
                    if (!IsCurrentSiblingForShowScheduling(sibling) ||
                        // 当前节点不具备阻挡资格，继续检查后续兄弟。
                        sibling.mutexGroup != mutexGroup) continue;

                    // 相等优先级继续保留后请求者获胜，较低优先级不会阻止当前请求。
                    if (sibling.mutexPriority <= mutexPriority) continue;

                    // 只有已经显示、执行有效隐藏或持有有效显示预约的兄弟才占用优先级。
                    if (sibling.IsHoldingMutexPriority())
                    {
                        // 保存实际阻挡当前请求的兄弟界面。
                        blockingSibling = sibling;
                        // 找到更高优先级持有者，确认显示请求被阻挡。
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
            // 读取当前父节点，决定扫描直属兄弟还是场景根节点。
            var parent = transform.parent;
            // 有父节点时兄弟范围限定为同一直接父节点。
            if (parent)
            {
                // 调度快照不过滤失活对象，具体是否占权由IsHoldingMutexPriority判定。
                for (var i = 0; i < parent.childCount; i++)
                {
                    // 取得当前待检查的直属子 Transform。
                    var child = parent.GetChild(i);
                    // 自身不参与兄弟互斥比较。
                    if (child == transform) continue;
                    // 将直接挂载 UIBase 的兄弟加入调度候选快照。
                    if (child.TryGetComponent<UIBase>(out var sibling)) siblings.Add(sibling);
                }

                // 已完成直属兄弟扫描，无需扫描场景根对象。
                return;
            }

            // 根节点仅与同一已加载Scene的其他根UI互为兄弟。
            var scene = gameObject.scene;
            // 场景未加载时不尝试取得根兄弟。
            if (!scene.isLoaded) return;
            // 借用临时列表保存场景根 GameObject。
            var rootObjects = ListPool<GameObject>.Get();
            // 保证场景根对象列表在扫描后归还。
            try
            {
                // 遍历Scene根对象时同样保留失活但可能有预约的UI。
                scene.GetRootGameObjects(rootObjects);
                // 按当前场景根列表检查兄弟候选。
                for (var i = 0; i < rootObjects.Count; i++)
                {
                    // 取得当前待检查的根对象。
                    var rootObject = rootObjects[i];
                    // 排除当前面板自身所在的根对象。
                    if (rootObject == gameObject) continue;
                    // 将根层级上的其他 UIBase 纳入兄弟候选。
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
            // 读取当前直接父节点，用于验证实时兄弟归属。
            var parent = transform.parent;
            // 有父节点时要求候选仍直属同一个父节点。
            if (parent) return sibling.transform.parent == parent;
            // 根层级兄弟必须仍属于同一场景且没有父节点。
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
            // 仅对当前有效父级联恢复来源父操作的版本。
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
                    // 本轮版本与显示准入均有效，允许继续显示。
                    allowScheduledParents: _allowScheduledParentsVersion == operationVersion)) return true;

            // 准入失效时通过隐藏收口本轮显示。
            return StopCurrentShow(operationVersion, operationCts);
        }

        /// <summary>父调用结束后确认实际结果；仅等待任务结束不能证明父已成功显示。</summary>
        /// <param name="parent">当前需要检查或记录的父界面。</param>
        /// <param name="operationVersion">用于确认本轮操作仍然有效的显隐版本。</param>
        /// <param name="operationCts">异步操作拥有的取消源；同步校验时为空。</param>
        /// <returns>当前操作有效且已调度父界面就绪时为真。</returns>
        private bool MaintainAutoParentResult(UIBase parent, int operationVersion,
            CancellationTokenSource operationCts = null)
        {
            // 本轮版本或令牌已经失效时停止祖先结果复核。
            if (!ValidateCurrentShowHideAction(operationVersion, operationCts)) return false;
            // 回调已改变层级时不再要求旧父，最新连续父链由阶段结束后的统一复核处理。
            if (!IsCurrentParent(parent)) return true;
            // 父界面完成显示关系调度即可供后续父链复用。
            if (parent.IsShowRelationReady(true))
                // 已调度父界面就绪，无需等待其动画。
                return true;

            // 记录自动父链未能成功就绪的具体父界面。
            LogShowRejection(ShowRejectionReason.AutoParentNotReady, UIShowMode.Normal, true, parent);
            // 父链失败后隐藏收口目标，而不回滚其他已打开祖先。
            return StopCurrentShow(operationVersion, operationCts);
        }

        /// <summary>通过同步隐藏收口失去准入的显示，并按调用方式返回或传播取消。</summary>
        /// <param name="operationVersion">用于确认本轮操作仍然有效的显隐版本。</param>
        /// <param name="operationCts">异步操作拥有的取消源；同步校验时为空。</param>
        /// <returns>同步收口时返回假；异步收口通过取消异常结束。</returns>
        private bool StopCurrentShow(int operationVersion, CancellationTokenSource operationCts)
        {
            // 新隐藏请求会递增版本并取消当前异步令牌，确保已启动显示行为能够完整收口。
            ExecuteHide();

            // 异步路径必须把更新取消传播给原显示调用方，不能把已开始的显示误报为成功。
            if (operationCts != null)
                // 异步调用在收口后以版本失效或取消结果结束。
                ThrowIfCurrentShowHideActionInvalid(operationCts, operationVersion);

            // 同步显示已经由新版本隐藏流程取代，旧调用栈只需停止继续执行。
            return false;
        }

        /// <summary>动画恢复只核验既有决策；新层级仍有可见竞争者时停止自己，不再调度隐藏别人。</summary>
        /// <param name="siblings">接收或保存本轮兄弟界面关系的列表。</param>
        /// <param name="showMode">当前显示请求来自普通调用还是父级联。</param>
        /// <param name="operationVersion">用于确认本轮操作仍然有效的显隐版本。</param>
        /// <param name="operationCts">异步操作拥有的取消源；同步校验时为空。</param>
        private void MaintainCommittedShowRelations(List<UIBase> siblings, UIShowMode showMode,
            int operationVersion, CancellationTokenSource operationCts)
        {
            // 完成阶段先复核目标及当前父链的显示资格。
            MaintainCurrentShowAdmission(showMode, operationVersion, operationCts);
            // 获取完成阶段的当前兄弟快照，仅用于核验已提交关系。
            FindSiblings(siblings);
            // 检查是否出现尚未隐藏的同组兄弟。
            foreach (var sibling in siblings)
            {
                // 排除无效关系、不同互斥组和已经隐藏的兄弟。
                if (!IsCurrentSibling(sibling) || sibling.mutexGroup != mutexGroup ||
                    // 当前兄弟不影响本轮已提交显示关系。
                    !RequiresSiblingHide(sibling)) continue;
                // 正在有效隐藏的兄弟继续按自己的操作收尾。
                if (sibling.State == UIState.Hiding && sibling.IsCurrentHideAction()) continue;
                // 记录完成阶段出现同组可见兄弟的关系变化。
                LogShowRejection(ShowRejectionReason.CommittedMutexChanged, showMode, true, sibling);
                // 当前显示失去已提交互斥条件，通过隐藏收口。
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
                // 每轮重新取得当前兄弟关系，以观察上一轮回调造成的变化。
                FindSiblings(siblings);

                // 每轮只选取一个当前竞争项，隐藏回调改变层级后不继续使用旧快照。
                UIBase siblingToHide = null;
                // 按当前快照寻找下一个必须隐藏的同组兄弟。
                for (var i = 0; i < siblings.Count; i++)
                {
                    // 取得当前互斥协调候选。
                    var eachSibling = siblings[i];
                    // 排除已换父、不同互斥组和无需再次隐藏的兄弟。
                    if (!IsCurrentSibling(eachSibling) ||
                        eachSibling.mutexGroup != mutexGroup ||
                        // 当前候选不需要隐藏，继续查找。
                        !RequiresSiblingHide(eachSibling)) continue;
                    // 保存本轮需要隐藏的第一个兄弟。
                    siblingToHide = eachSibling;
                    // 找到目标后先执行一次隐藏，再重新扫描关系。
                    break;
                }

                // 没有剩余竞争项时再次复核准入，并保留本轮最新兄弟快照供置顶使用。
                if (!siblingToHide)
                    // 没有剩余隐藏目标时返回最终显示准入结果。
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
            // 借用任务列表跟踪已启动的异步互斥隐藏。
            var tasks = ListPool<UniTask>.Get();
            // 无论协调成功或中断，都归还关联任务列表。
            try
            {
                // 单独保存同步互斥调度阶段的失败。
                Exception schedulingException = null;
                // 调度中断后仍必须等待此前已启动的兄弟隐藏任务。
                try
                {
                    // 同步完成关系调度；每次隐藏后的同步回调仍可能换父，因此重新取快照。
                    while (true)
                    {
                        // 每轮隐藏启动前复核当前显示操作和准入。
                        MaintainCurrentShowAdmission(showMode, operationVersion, operationCts);
                        // 根据最新 Transform 层级刷新兄弟快照。
                        FindSiblings(siblings);
                        // 清除本轮待隐藏目标，随后重新寻找。
                        UIBase toHide = null;
                        // 遍历当前兄弟快照寻找同组可见节点。
                        foreach (var sibling in siblings)
                        {
                            // 排除无效关系、不同互斥组和已完成隐藏的节点。
                            if (!IsCurrentSibling(sibling) || sibling.mutexGroup != mutexGroup ||
                                // 当前兄弟不需要本轮隐藏处理。
                                !RequiresSiblingHide(sibling)) continue;
                            // 隐藏目标已经提交，动画尚未结束不需要重复隐藏或重启动画。
                            if (sibling.State == UIState.Hiding && sibling.IsCurrentHideAction()) continue;
                            // 选中当前需要隐藏的兄弟。
                            toHide = sibling;
                            // 每轮只启动一个目标，然后重新核对关系变化。
                            break;
                        }
                        // 没有新的互斥隐藏目标时结束启动循环。
                        if (!toHide) break;
                        // 启动兄弟异步隐藏并将结果纳入本轮观察集合。
                        tasks.Add(toHide.ExecuteHideAsync(operationCts.Token));
                        // 同步抛错后留下的 Hiding 不可在这一轮被无限重试。
                        if (toHide && toHide.State != UIState.Hided && !toHide.IsCurrentHideAction()) break;
                    }
                }
                // 保存互斥调度阶段异常，继续处理已启动任务的收尾。
                catch (Exception exception) { schedulingException = exception; }
                // 单独保存兄弟隐藏任务的最终失败。
                Exception presentationException = null;
                // 等待所有已启动互斥隐藏任务收尾后再传播异常。
                try { await UIOperationDispatcher.Continue(UIAsync.WaitAll(tasks)); }
                // 记录关联隐藏任务报告的异常。
                catch (Exception exception) { presentationException = exception; }
                // 按统一规则传播调度和隐藏表现的异常。
                ThrowHandlerExceptions(schedulingException, presentationException);
            }
            // 归还本轮互斥隐藏任务列表。
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

using UnityEngine.Pool;

namespace JH
{
    /// <summary>UI面板的只读显示预演与已调度父链准入判断。</summary>
    public partial class UIBase
    {
        // 关系调度完成与动画完成分别登记；Showing 也可以具有已提交的显示关系。
        /// <summary>最近一次完成显示关系调度的操作版本，可早于动画完成。</summary>
        private int _showRelationsVersion = -1;
        /// <summary>允许复用已调度祖先状态的当前显示操作版本。</summary>
        private int _allowScheduledParentsVersion = -1;

        /// <summary>检查面板是否已初始化、激活并完成要求的显示关系阶段。</summary>
        /// <param name="allowTransition">是否接受仍处于 Showing 但已完成显示关系调度的状态。</param>
        /// <returns>面板满足当前要求的显示关系状态时为真。</returns>
        private bool IsShowRelationReady(bool allowTransition)
        {
            // 将存活、初始化、激活与所需显示阶段共同作为就绪条件。
            return this && _isUIStateInitialized && gameObject.activeInHierarchy &&
                   (State == UIState.Showed || allowTransition && IsCurrentShowAction() &&
                       _showRelationsVersion == _showHideOperationVersion);
        }

        /// <summary>
        /// 只读检查本次必须显示的祖先和目标。不执行生命周期、取消、动画或可选子节点联动。
        /// 已计划打开的祖先可以满足后续严格祖先的条件，不能只检查当前状态。
        /// </summary>
        /// <param name="allowScheduledParents">是否允许复用已完成关系调度但动画尚未结束的祖先。</param>
        /// <returns>目标和所有必要祖先均可被本次请求显示时为真。</returns>
        private bool CanPreflightShow(bool allowScheduledParents = false)
        {
            // 借用列表保存本次必须显示的祖先和目标。
            var chain = ListPool<UIBase>.Get();
            // 借用集合记录预演中已经计划打开的界面。
            var planned = HashSetPool<UIBase>.Get();
            // 预演拒绝或成功都必须归还临时关系集合。
            try
            {
                // 自动父链策略需要先加入连续祖先。
                if (ShouldAutoShowParentChain(UIShowMode.Normal)) FindParents(chain);
                // 将目标放在父链末尾，按祖先到目标的顺序检查。
                chain.Add(this);
                // 依次检查必须参与本次显示的界面。
                foreach (var panel in chain)
                {
                    // 已就绪的祖先不重新请求，也不会重新触发其默认子面板。
                    if (panel != this && panel.IsShowRelationReady(allowScheduledParents))
                    {
                        // 已经就绪的祖先可满足后续严格子链条件。
                        planned.Add(panel);
                        // 复用就绪祖先，不再检查它的重复显示调度。
                        continue;
                    }

                    // 严格策略额外要求完整连续父链就绪。
                    if (panel.parentShowPolicy == UIParentShowPolicy.RequireParentChainShowedForDirectShow)
                    {
                        // 从当前面板的直接父节点开始检查严格父链。
                        var parent = panel.transform.parent;
                        // 缺少直接父界面时严格显示无法获准。
                        if (!parent || !parent.TryGetComponent<UIBase>(out _))
                            // 记录并返回缺少直接父界面的预演拒绝。
                            return RejectPreflight(panel, ShowRejectionReason.MissingRequiredParent);
                        // 只沿连续挂载 UIBase 的父 Transform 链检查。
                        while (parent && parent.TryGetComponent<UIBase>(out var parentUI))
                        {
                            // 当前尚未就绪且本轮也未计划打开的祖先阻挡严格显示。
                            if (!planned.Contains(parentUI) && !parentUI.IsShowRelationReady(allowScheduledParents))
                                // 记录阻挡祖先并拒绝当前预演。
                                return RejectPreflight(panel, ShowRejectionReason.RequiredParentNotReady, parentUI);
                            // 继续检查更上一级祖先。
                            parent = parent.parent;
                        }
                    }

                    // 同父同组的更高优先级持有者可阻止当前显示。
                    if (panel.HasHigherPrioritySiblingHoldingMutex(out var blocker))
                        // 记录阻挡兄弟并拒绝本次预演。
                        return RejectPreflight(panel, ShowRejectionReason.HigherPrioritySibling, blocker);
                    // 当前面板通过预演后纳入后续祖先条件计算。
                    planned.Add(panel);
                }
                // 必要祖先和目标均通过检查，允许进入真实调度。
                return true;
            }
            finally
            {
                // 归还本轮计划显示集合。
                HashSetPool<UIBase>.Release(planned);
                // 归还本轮必要父链列表。
                ListPool<UIBase>.Release(chain);
            }
        }

        /// <summary>记录目标或必要祖先的预演拒绝原因，并返回准入失败。</summary>
        /// <param name="panel">当前操作或诊断针对的界面。</param>
        /// <param name="reason">导致本次显示准入失败的原因。</param>
        /// <param name="related">造成预演失败的关联界面，没有时为空。</param>
        /// <returns>固定返回假，表示本次显示预演未通过。</returns>
        private bool RejectPreflight(UIBase panel, ShowRejectionReason reason, UIBase related = null)
        {
            // 首先在实际失败面板上记录具体拒绝原因。
            panel.LogShowRejection(reason, UIShowMode.Normal, false, related);
            // 失败发生于必要祖先时，还需向目标说明整次预演被拒绝。
            if (panel != this)
                // 将必要祖先失败关联到原始目标请求。
                LogShowRejection(ShowRejectionReason.PreflightAncestorRejected, UIShowMode.Normal, false, panel);
            // 固定返回预演失败，不启动任何生命周期副作用。
            return false;
        }
    }
}

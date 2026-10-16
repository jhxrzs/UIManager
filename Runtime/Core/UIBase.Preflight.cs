using System.Collections.Generic;
using UnityEngine.Pool;

namespace JH
{
    public partial class UIBase
    {
        // 关系调度完成与动画完成分别登记；Showing 也可以具有已提交的显示关系。
        private int _showRelationsVersion = -1;
        private int _allowScheduledParentsVersion = -1;

        private bool IsShowRelationReady(bool allowTransition)
        {
            return this && _isUIStateInitialized && gameObject.activeInHierarchy &&
                   (State == UIState.Showed || allowTransition && IsCurrentShowAction() &&
                       _showRelationsVersion == _showHideOperationVersion);
        }

        /// <summary>
        /// 只读检查本次必须显示的祖先和目标。不执行生命周期、取消、动画或可选子节点联动。
        /// 已计划打开的祖先可以满足后续严格祖先的条件，不能只检查当前状态。
        /// </summary>
        private bool CanPreflightShow(bool allowScheduledParents = false)
        {
            var chain = ListPool<UIBase>.Get();
            var planned = HashSetPool<UIBase>.Get();
            try
            {
                if (ShouldAutoShowParentChain(UIShowMode.Normal)) FindParents(chain);
                chain.Add(this);
                foreach (var panel in chain)
                {
                    // 已经完成显示的祖先不重新请求，也不会重新触发其默认子面板。
                    if (panel != this && panel.IsShowRelationReady(allowScheduledParents))
                    {
                        planned.Add(panel);
                        continue;
                    }

                    if (panel.parentShowPolicy == UIParentShowPolicy.RequireParentChainShowedForDirectShow)
                    {
                        var parent = panel.transform.parent;
                        if (!parent || !parent.TryGetComponent<UIBase>(out _))
                            return RejectPreflight(panel, ShowRejectionReason.MissingRequiredParent);
                        while (parent && parent.TryGetComponent<UIBase>(out var parentUI))
                        {
                            if (!planned.Contains(parentUI) && !parentUI.IsShowRelationReady(allowScheduledParents))
                                return RejectPreflight(panel, ShowRejectionReason.RequiredParentNotReady, parentUI);
                            parent = parent.parent;
                        }
                    }

                    if (panel.HasHigherPrioritySiblingHoldingMutex(out var blocker))
                        return RejectPreflight(panel, ShowRejectionReason.HigherPrioritySibling, blocker);
                    planned.Add(panel);
                }
                return true;
            }
            finally
            {
                HashSetPool<UIBase>.Release(planned);
                ListPool<UIBase>.Release(chain);
            }
        }

        private bool RejectPreflight(UIBase panel, ShowRejectionReason reason, UIBase related = null)
        {
            panel.LogShowRejection(reason, UIShowMode.Normal, false, related);
            if (panel != this)
                LogShowRejection(ShowRejectionReason.PreflightAncestorRejected, UIShowMode.Normal, false, panel);
            return false;
        }

    }
}

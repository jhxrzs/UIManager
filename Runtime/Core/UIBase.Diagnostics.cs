using UnityEngine;

namespace JH
{
    public partial class UIBase
    {
        private enum ShowRejectionReason
        {
            MissingRequiredParent,
            RequiredParentNotReady,
            AutoParentNotReady,
            PreflightAncestorRejected,
            CommittedMutexChanged,
            HigherPrioritySibling,
            CascadeSourceUnavailable,
            CascadeParentChanged,
            CascadeOperationReplaced
        }

        /// <summary>仅在真实准入失败时记录一次；关闭开关时不构建日志字符串或层级路径。</summary>
        private void LogShowRejection(ShowRejectionReason reason, UIShowMode showMode, bool isRevalidation,
            UIBase relatedPanel = null)
        {
            if (!UIOperationSettings.EnableSchedulingLogs) return;

            var explanation = reason switch
            {
                ShowRejectionReason.MissingRequiredParent =>
                    $"严格父链条件不满足：直接父节点「{GetSchedulingPath(transform.parent)}」未挂载 UIBase，" +
                    "需要存在直接 UI 父节点。",
                ShowRejectionReason.RequiredParentNotReady =>
                    $"严格父链条件不满足：父链面板「{GetSchedulingPath(relatedPanel.transform)}」尚未就绪" +
                    $"（状态={relatedPanel.State}，已初始化={relatedPanel._isUIStateInitialized}，" +
                    $"层级激活={relatedPanel.gameObject.activeInHierarchy}）。",
                ShowRejectionReason.HigherPrioritySibling =>
                    $"互斥组 {mutexGroup} 中面板「{GetSchedulingPath(relatedPanel.transform)}」持有更高优先级；" +
                    $"目标优先级 {mutexPriority}，持有者优先级 {relatedPanel.mutexPriority}，" +
                    $"持有者状态={relatedPanel.State}。",
                ShowRejectionReason.AutoParentNotReady =>
                    $"自动父链未就绪：父链面板「{GetSchedulingPath(relatedPanel.transform)}」未成功完成显示" +
                    $"（状态={relatedPanel.State}，已初始化={relatedPanel._isUIStateInitialized}，" +
                    $"层级激活={relatedPanel.gameObject.activeInHierarchy}）。",
                ShowRejectionReason.CascadeSourceUnavailable =>
                    "父级联来源不存在或已销毁。",
                ShowRejectionReason.PreflightAncestorRejected =>
                    $"显示预演失败：必要祖先「{GetSchedulingPath(relatedPanel.transform)}」无法显示，本次请求未执行。",
                ShowRejectionReason.CommittedMutexChanged =>
                    $"显示关系已变化：当前同组面板「{GetSchedulingPath(relatedPanel.transform)}」仍可见，停止本次显示，不重新调度该面板。",
                ShowRejectionReason.CascadeParentChanged =>
                    $"父级联关系已失效：来源父面板为「{GetSchedulingPath(relatedPanel.transform)}」，" +
                    $"当前直接父节点为「{GetSchedulingPath(transform.parent)}」。",
                ShowRejectionReason.CascadeOperationReplaced =>
                    $"来源父面板「{GetSchedulingPath(relatedPanel.transform)}」的显示操作已被后续操作取代。",
                _ => "显示准入条件不满足。"
            };
            var outcome = isRevalidation ? "显示操作失去准入，准备停止并收口" : "显示请求被拒绝";
            var source = showMode == UIShowMode.ParentCascade ? "随父显示" : "普通显示";
            Debug.Log($"[UIManager] 面板「{GetSchedulingPath(transform)}」{outcome}（{source}）：{explanation}", this);
        }

        private static string GetSchedulingPath(Transform node)
        {
            if (!node) return "<不存在>";
            var path = node.name;
            for (var parent = node.parent; parent; parent = parent.parent) path = parent.name + "/" + path;
            return node.gameObject.scene.name + ":/" + path;
        }
    }
}

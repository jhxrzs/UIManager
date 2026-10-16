using UnityEngine;

namespace JH
{
    /// <summary>UI面板的显示准入拒绝原因和层级诊断。</summary>
    public partial class UIBase
    {

        /// <summary>仅在真实准入失败时记录一次；关闭开关时不构建日志字符串或层级路径。</summary>
        /// <param name="reason">导致本次显示准入失败的原因。</param>
        /// <param name="showMode">当前显示请求来自普通调用还是父级联。</param>
        /// <param name="isRevalidation">是否在已开始的显示过程中重新检查准入。</param>
        /// <param name="relatedPanel">与拒绝原因相关的父界面或兄弟界面，没有时为空。</param>
        private void LogShowRejection(ShowRejectionReason reason, UIShowMode showMode, bool isRevalidation,
            UIBase relatedPanel = null)
        {
            // 诊断开关关闭时不构造日志内容。
            if (!UIOperationSettings.EnableSchedulingLogs) return;

            // 按拒绝类型生成包含关联界面和准入条件的解释。
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
            // 区分入口直接拒绝与已开始显示后的准入失效。
            var outcome = isRevalidation ? "显示操作失去准入，准备停止并收口" : "显示请求被拒绝";
            // 标明该请求来自普通显示还是父级联。
            var source = showMode == UIShowMode.ParentCascade ? "随父显示" : "普通显示";
            // 将原因、目标路径和请求来源写入关联当前面板的日志。
            Debug.Log($"[UIManager] 面板「{GetSchedulingPath(transform)}」{outcome}（{source}）：{explanation}", this);
        }

        /// <summary>构建包含场景名的层级路径，用于定位准入诊断涉及的节点。</summary>
        /// <param name="node">需要构建场景层级路径的节点。</param>
        /// <returns>包含场景名的层级路径；节点不存在时返回占位说明。</returns>
        private static string GetSchedulingPath(Transform node)
        {
            // 不存在的节点使用明确占位说明。
            if (!node) return "<不存在>";
            // 以目标节点名称开始拼接层级路径。
            var path = node.name;
            // 逐层添加父节点名称，形成完整 Transform 路径。
            for (var parent = node.parent; parent; parent = parent.parent) path = parent.name + "/" + path;
            // 在层级路径前附加场景名，区分不同场景的同名节点。
            return node.gameObject.scene.name + ":/" + path;
        }
    }
}

namespace JH
{
    /// <summary>UI面板的分部定义，保存内部调度类型。</summary>
    public partial class UIBase
    {
        /// <summary>显示预演或运行期准入复核可能遇到的拒绝原因。</summary>
        private enum ShowRejectionReason
        {
            /// <summary>严格策略要求的直接父界面不存在。</summary>
            MissingRequiredParent,
            /// <summary>严格策略要求的连续父链尚未完整显示。</summary>
            RequiredParentNotReady,
            /// <summary>自动补齐的父链仍未达到可继续显示的状态。</summary>
            AutoParentNotReady,
            /// <summary>必要祖先未通过显示预演，目标请求一并拒绝。</summary>
            PreflightAncestorRejected,
            /// <summary>已提交显示关系中的互斥条件在完成阶段发生变化。</summary>
            CommittedMutexChanged,
            /// <summary>同父同组兄弟持有更高显示优先级。</summary>
            HigherPrioritySibling,
            /// <summary>父级联显示的来源界面已不存在。</summary>
            CascadeSourceUnavailable,
            /// <summary>目标界面的当前直接父节点已不是原级联来源。</summary>
            CascadeParentChanged,
            /// <summary>来源父界面的显示操作已被后续操作替代。</summary>
            CascadeOperationReplaced
        }
    }
}

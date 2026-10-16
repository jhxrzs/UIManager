using System;

namespace JH
{
    /// <summary>同一调度来源链中，面板的某种显隐行为实际执行次数超过配置上限。</summary>
    public sealed class UIRequestLoopException : InvalidOperationException
    {
        /// <summary>被阻止的请求目标。</summary>
        public UIBase Panel { get; }

        /// <summary>被阻止的行为是否为显示；同步和异步接口使用同一个计数。</summary>
        public bool IsShow { get; }

        /// <summary>请求创建时保存的面板层级路径。</summary>
        public string PanelPath { get; }

        /// <summary>该来源链允许的实际执行次数。</summary>
        public int ExecutionLimit { get; }

        /// <summary>从根请求到被阻止请求的完整调度路径。</summary>
        public string RequestPath { get; }

        /// <summary>请求入队时的调用栈；Editor 和 Development Build 中提供。</summary>
        public string RequestOrigin { get; }

        /// <summary>保存被循环保护阻止的目标、方向、限制和请求来源诊断。</summary>
        /// <param name="panel">当前操作或诊断针对的界面。</param>
        /// <param name="isShow">显示方向为真，隐藏方向为假。</param>
        /// <param name="panelPath">请求创建时保存的面板路径。</param>
        /// <param name="executionLimit">当前来源链允许的实际执行次数。</param>
        /// <param name="requestPath">从根请求到被阻止请求的诊断路径。</param>
        /// <param name="requestOrigin">被阻止请求提交位置的调用栈。</param>
        internal UIRequestLoopException(UIBase panel, bool isShow, string panelPath, int executionLimit,
            string requestPath, string requestOrigin)
            : base($"UI循环调度次数超限：面板「{panelPath}」的 {(isShow ? "Show" : "Hide")} " +
                   $"在同一来源链中最多实际执行 {executionLimit} 次，第 {executionLimit + 1L} 次已被阻止。\n" +
                   $"调度来源链：\n{requestPath}" +
                   (string.IsNullOrEmpty(requestOrigin) ? string.Empty : $"\n被阻止请求的提交位置：\n{requestOrigin}"))
        {
            // 保存被循环保护拒绝的目标界面。
            Panel = panel;
            // 保存被拒绝行为的显隐方向。
            IsShow = isShow;
            // 保存请求提交时的面板路径。
            PanelPath = panelPath;
            // 保存该来源链使用的执行次数限制。
            ExecutionLimit = executionLimit;
            // 保存从根请求到当前被拒绝请求的完整来源。
            RequestPath = requestPath;
            // 保存被拒绝请求的提交调用栈。
            RequestOrigin = requestOrigin;
        }
    }
}

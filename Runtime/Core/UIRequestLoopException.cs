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

        internal UIRequestLoopException(UIBase panel, bool isShow, string panelPath, int executionLimit,
            string requestPath, string requestOrigin)
            : base($"UI循环调度次数超限：面板「{panelPath}」的 {(isShow ? "Show" : "Hide")} " +
                   $"在同一来源链中最多实际执行 {executionLimit} 次，第 {executionLimit + 1L} 次已被阻止。\n" +
                   $"调度来源链：\n{requestPath}" +
                   (string.IsNullOrEmpty(requestOrigin) ? string.Empty : $"\n被阻止请求的提交位置：\n{requestOrigin}"))
        {
            Panel = panel;
            IsShow = isShow;
            PanelPath = panelPath;
            ExecutionLimit = executionLimit;
            RequestPath = requestPath;
            RequestOrigin = requestOrigin;
        }
    }
}

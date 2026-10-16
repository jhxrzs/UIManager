using System;

namespace JH
{
    /// <summary>显隐调度器的分部定义，保存当前文件对应的内部类型。</summary>
    internal static partial class UIOperationDispatcher
    {
        /// <summary>请求的因果关系不依赖消费器是否空闲，异步恢复仍保留这条链。</summary>
        internal sealed class RequestContext
        {
            /// <summary>当前请求针对的界面实例。</summary>
            internal readonly UIBase Panel;
            // Switch 在入队时尚未确定方向，首次实际执行时再固定为 Show 或 Hide。
            /// <summary>请求的显隐方向；切换请求在首次实际执行前保持未确定。</summary>
            internal bool? IsShow;
            /// <summary>产生当前请求的直接来源请求，根请求为空。</summary>
            internal readonly RequestContext Parent;
            /// <summary>从根请求继承的单种行为执行次数上限。</summary>
            internal readonly int ExecutionLimit;
            /// <summary>请求创建时保存的目标面板层级路径。</summary>
            internal readonly string PanelPath;
            /// <summary>编辑器或开发构建中保存的请求提交调用栈。</summary>
            internal readonly string Origin;
            /// <summary>该请求是否已通过入口检查并计入真实行为执行次数。</summary>
            internal bool HasStarted;

            /// <summary>创建请求来源记录，并继承执行限制及保存诊断信息。</summary>
            /// <param name="panel">当前操作或诊断针对的界面。</param>
            /// <param name="isShow">显示方向为真，隐藏方向为假；尚未确定的切换请求为空。</param>
            /// <param name="parent">产生当前请求的来源请求；独立根请求为空。</param>
            internal RequestContext(UIBase panel, bool? isShow, RequestContext parent)
            {
                // 保存当前请求针对的界面。
                Panel = panel;
                // 保存请求方向，Switch 可在实际执行时再确定。
                IsShow = isShow;
                // 保存直接来源请求以建立因果链。
                Parent = parent;
                // 继承根请求执行上限，独立根请求读取当前公共配置。
                ExecutionLimit = parent?.ExecutionLimit ?? UIOperationSettings.MaxExecutionsPerBehavior;
                // 在创建时固定目标路径，保留运行中换父前的诊断信息。
                PanelPath = GetPanelPath(panel);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // 在编辑器或开发构建中保存请求提交的调用栈。
                Origin = new System.Diagnostics.StackTrace(2, true).ToString();
#else
                // 非开发构建不采集调用栈，避免运行时诊断开销。
                Origin = null;
#endif
            }
        }
    }
}

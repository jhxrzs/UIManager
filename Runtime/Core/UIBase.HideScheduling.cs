using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace JH
{
    /// <summary>UI面板递归隐藏时使用的资格上下文定义。</summary>
    public partial class UIBase
    {
        /// <summary>
        /// 保存一次递归隐藏中已取得的父子关系资格。只补充失活前的资格，不替代当前层级扫描。
        /// </summary>
        private sealed class UIHideContext : IDisposable
        {
            /// <summary>本轮已取得隐藏资格的直属父子关系集合，使用后归还对象池。</summary>
            private HashSet<(UIBase Parent, UIBase Child)> admittedChildren =
                HashSetPool<(UIBase Parent, UIBase Child)>.Get();

            /// <summary>在生命周期回调之前创建隐藏上下文并捕获当前合格子树。</summary>
            /// <param name="root">本轮开始递归隐藏的分支根界面。</param>
            /// <returns>由调用方负责释放的本轮隐藏资格上下文。</returns>
            internal static UIHideContext Capture(UIBase root)
            {
                // 创建持有池化父子关系集合的本轮上下文。
                var context = new UIHideContext();
                // 捕获过程中失败时也必须归还刚借出的集合。
                try
                {
                    // 在业务回调开始前记录根分支中已取得资格的关系。
                    context.CaptureChildren(root);
                    // 将捕获完成的上下文交给隐藏流程管理生命周期。
                    return context;
                }
                catch
                {
                    // 捕获失败时归还资格集合，避免池资源遗留。
                    context.Dispose();
                    // 保留捕获失败的原始异常。
                    throw;
                }
            }

            /// <summary>判断指定直属关系是否已在本轮取得隐藏资格。</summary>
            /// <param name="parent">当前需要检查或记录的父界面。</param>
            /// <param name="child">当前需要检查或记录的直接子界面。</param>
            /// <returns>指定父子关系已取得本轮隐藏资格时为真。</returns>
            internal bool Contains(UIBase parent, UIBase child) => admittedChildren.Contains((parent, child));

            /// <summary>记录一条新取得资格的父子关系，并捕获未隐藏子分支的后代。</summary>
            /// <param name="parent">当前需要检查或记录的父界面。</param>
            /// <param name="child">当前需要检查或记录的直接子界面。</param>
            internal void Include(UIBase parent, UIBase child)
            {
                // 每条关系只预扫描一次。Hided节点仍由ExecuteHide处理预约取消，不穿透状态短路。
                if (!admittedChildren.Add((parent, child)) || child.State == UIState.Hided) return;
                // 对新取得资格且未隐藏的分支继续捕获直接后代。
                CaptureChildren(child);
            }

            /// <summary>扫描当前直属活动子界面和有效显示预约，补充尚未记录的关系。</summary>
            /// <param name="parent">当前需要检查或记录的父界面。</param>
            internal void CaptureChildren(UIBase parent)
            {
                // 沿连续UI关系捕获，不穿过普通Transform或入口已失活且无显示预约的分支。
                var parentTransform = parent.transform;
                // 按当前层级顺序检查直接子 Transform。
                for (var i = 0; i < parentTransform.childCount; i++)
                {
                    // 取得当前待检查的直接子节点。
                    var child = parentTransform.GetChild(i);
                    // 普通 Transform 中断连续 UI 隐藏关系。
                    if (!child.TryGetComponent<UIBase>(out var childUI)) continue;
                    // 仅活动子界面或有效显示预约能够新增隐藏资格。
                    if (child.gameObject.activeInHierarchy || childUI.IsCurrentShowAction())
                        // 记录当前父子关系，并仅对新关系递归补齐后代。
                        Include(parent, childUI);
                }
            }

            /// <summary>归还隐藏资格集合并清空引用，重复释放不产生副作用。</summary>
            public void Dispose()
            {
                // 已释放上下文不再重复归还集合。
                if (admittedChildren == null) return;
                // 将本轮父子关系集合清空并归还共享池。
                HashSetPool<(UIBase Parent, UIBase Child)>.Release(admittedChildren);
                // 清除集合引用，阻止后续重复释放影响池复用。
                admittedChildren = null;
            }
        }
    }
}

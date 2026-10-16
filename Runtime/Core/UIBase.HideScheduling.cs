using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace JH
{
    public partial class UIBase
    {
        /// <summary>
        /// 保存一次递归隐藏中已取得的父子关系资格。只补充失活前的资格，不替代当前层级扫描。
        /// </summary>
        private sealed class UIHideContext : IDisposable
        {
            private HashSet<(UIBase Parent, UIBase Child)> admittedChildren =
                HashSetPool<(UIBase Parent, UIBase Child)>.Get();

            internal static UIHideContext Capture(UIBase root)
            {
                var context = new UIHideContext();
                try
                {
                    context.CaptureChildren(root);
                    return context;
                }
                catch
                {
                    context.Dispose();
                    throw;
                }
            }

            internal bool Contains(UIBase parent, UIBase child) => admittedChildren.Contains((parent, child));

            internal void Include(UIBase parent, UIBase child)
            {
                // 每条关系只预扫描一次。Hided节点仍由ExecuteHide处理预约取消，不穿透状态短路。
                if (!admittedChildren.Add((parent, child)) || child.State == UIState.Hided) return;
                CaptureChildren(child);
            }

            internal void CaptureChildren(UIBase parent)
            {
                // 沿连续UI关系捕获，不穿过普通Transform或入口已失活且无显示预约的分支。
                var parentTransform = parent.transform;
                for (var i = 0; i < parentTransform.childCount; i++)
                {
                    var child = parentTransform.GetChild(i);
                    if (!child.TryGetComponent<UIBase>(out var childUI)) continue;
                    if (child.gameObject.activeInHierarchy || childUI.IsCurrentShowAction())
                        Include(parent, childUI);
                }
            }

            public void Dispose()
            {
                if (admittedChildren == null) return;
                HashSetPool<(UIBase Parent, UIBase Child)>.Release(admittedChildren);
                admittedChildren = null;
            }
        }
    }
}

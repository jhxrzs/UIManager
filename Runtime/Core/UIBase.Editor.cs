#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JH
{
    public partial class UIBase
    {
        /// <summary>
        ///  寻找所有的父面板，从祖先到父亲
        /// </summary>
        partial void FindParents()
        {
            //循环找到所有的父面板
            parents = new List<UIBase>();
            var current = transform.parent;
            while (current != null)
            {
                var uiBase = current.GetComponent<UIBase>();
                if (uiBase != null)
                {
                    parents.Add(uiBase);
                }
                else
                {
                    break;
                }

                current = current.parent;
            }

            //反转父面板集合，从祖先到父亲
            parents.Reverse();
        }

        /// <summary>
        ///  寻找所有的兄弟面板，和最大的兄弟节点索引
        /// </summary>
        partial void FindSiblings()
        {
            //尝试找到父节点
            var parent = transform.parent;

            //所有的兄弟节点
            siblings = new List<UIBase>();

            //如果没有父节点，那么就是根节点，那么就找到所有的根节点,并且排除自己
            if (parent == null)
            {
                var scene = gameObject.scene;
                if (!scene.isLoaded)
                {
                    siblings = new List<UIBase>();
                    return;
                }

                // 获取所有的兄弟节点,并且排除自己和未激活的节点
                siblings = scene.GetRootGameObjects()
                    .Select(i => i.GetComponent<UIBase>())
                    .Where(i => i != null && i != this && i.gameObject.activeInHierarchy).ToList();

                // 获取最大的兄弟节点索引，仅限于未固定兄弟节点索引的节点
                if (siblings.Count > 0)
                    maxSiblingIndex = Mathf.Max(siblings
                        .Where(i => !i.fixedSiblingIndex)
                        .Max(i => i.transform.GetSiblingIndex()), transform.GetSiblingIndex());

                return;
            }

            //如果有父面板，那么就找到所有的子面板，并且排除自己
            for (int i = 0; i < transform.parent.childCount; i++)
            {
                var child = transform.parent.GetChild(i);

                //过滤掉未激活的节点
                if (!child.gameObject.activeInHierarchy) continue;

                if (child != transform)
                {
                    var uiBase = child.GetComponent<UIBase>();
                    if (uiBase != null)
                    {
                        siblings.Add(uiBase);
                    }
                }
            }

            // 获取最大的兄弟节点索引
            if (siblings.Count > 0)
                maxSiblingIndex = siblings.Max(i => i.transform.GetSiblingIndex());
        }

        /// <summary>
        /// 初始化UI面板的子面板集合
        /// </summary>
        partial void FindChildren()
        {
            children = GetComponentsInChildren<UIBase>()
                .Where(i => i.transform.parent == transform)
                .Where(i => i != null && i.gameObject.activeInHierarchy).ToList();
        }

        #region 测试函数

        [ContextMenu("测试显示UI")]
        public void TestShow()
        {
            Show();
        }

        [ContextMenu("测试隐藏UI")]
        public void TestHide()
        {
            Hide();
        }

        [ContextMenu("测试异步显示UI")]
        public void TestShowAsync()
        {
            _ = ShowAsync(destroyCancellationToken);
        }

        [ContextMenu("测试异步隐藏UI")]
        public void TestHideAsync()
        {
            _ = HideAsync(destroyCancellationToken);
        }

        [ContextMenu("测试切换UI")]
        public void TestSwitch(bool isShow)
        {
            if (isShow)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }

        [ContextMenu("测试异步切换UI")]
        public void TestSwitchAsync(bool isShow)
        {
            _ = isShow ? ShowAsync(destroyCancellationToken) : HideAsync(destroyCancellationToken);
        }

        #endregion
    }
}
#endif
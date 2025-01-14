using System.Collections.Generic;
using System.Linq;
using FuzzyMatching;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace UIManager
{
    /// <summary>
    ///  使用模糊匹配，尝试自动序列化<see cref="RectTransformShow"/>的RectTransformShow.target的值
    ///  和<see cref="RectTransformTweenShow"/>的RectTransformTweenShow.target的值
    /// </summary>
    public static class FuzzyFIndTransformTool
    {
        /// <summary>
        ///  目标字段名称
        /// </summary>
        private const string TargetName = "target";

        /// <summary>
        ///  尝试获取目标位置
        ///   使用模糊匹配，尝试自动序列化<see cref="RectTransformShow"/>的<see cref="RectTransformShow.target"/>的值
        /// 和<see cref="RectTransformTweenShow"/>的<see cref="RectTransformTweenShow.target"/>的值
        /// </summary>
        public static void TryGetTarget(MonoBehaviour mono)
        {
#if UNITY_EDITOR
            // 使用SerializedObject来获取序列化字段
            SerializedObject serializedObject = new SerializedObject(mono);
            SerializedProperty targetProperty = serializedObject.FindProperty(TargetName);
            if (targetProperty == null) return;

            // 约定，如果以Show结尾，则表示mono是用来显示UI的，否则是隐藏
            var isHide = !mono.GetType().Name.EndsWith("Show");

            // 拼接目标名称
            var target = mono.name + " " + (isHide ? "隐藏" : "显示");

            //是否有父物体
            var parent = mono.transform.parent;

            // 获取兄弟节点
            var siblings = GetSiblings(mono, parent);

            // 如果没有兄弟节点，直接返回
            if (siblings == null) return;

            // 使用模糊匹配，寻找目标名称
            var find = siblings.FuzzyMatch(target, t => t.name);

            //如果找到
            if (find != null)
            {
                // 使用SerializedProperty来设置值
                targetProperty.objectReferenceValue = find.GetComponent<RectTransform>();
                serializedObject.ApplyModifiedProperties();
            }
#endif
        }

        /// <summary>
        ///  获取mono的兄弟节点（只获取同层级的节点）
        /// </summary>
        private static List<Transform> GetSiblings(MonoBehaviour mono, Transform parent)
        {
            // 有父物体的情况
            if (parent != null)
            {
                var siblings = new List<Transform>();
                
                // 只获取第一层子物体
                for (var i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    // 排除自身
                    if (child != mono.transform)
                    {
                        siblings.Add(child);
                    }
                }

                return siblings;
            }

            // 没有父物体，检查场景
            if (string.IsNullOrWhiteSpace(mono.gameObject.scene.path))
            {
                return null; // 场景为空，不处理
            }

            // 获取场景根物体（排除自身）
            return mono.gameObject.scene.GetRootGameObjects()
                .Where(i => i.transform != mono.transform)
                .Select(i => i.transform)
                .ToList();
        }
    }
}
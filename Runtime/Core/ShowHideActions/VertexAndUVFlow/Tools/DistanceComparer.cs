using System.Collections.Generic;
using UnityEngine;

namespace JH
{
    public partial class UIFlowGroupController
    {
        /// <summary>
        ///  按子物体到统一终点的世界空间距离排序流动网格。
        /// </summary>
        private readonly struct DistanceComparer : IComparer<UIFlowMeshEffect>
        {
            /// <summary>
            ///  当前统一终点转换后的世界坐标。
            /// </summary>
            private readonly Vector3 _endPointWorld;

            /// <summary>
            ///  创建一个只读的世界空间距离比较器。
            /// </summary>
            /// <param name="endScreenPoint">当前统一主路径终点的屏幕坐标。</param>
            /// <param name="uiCamera">当前Canvas对应的相机；Overlay模式为空。</param>
            /// <param name="rectTransform">用于解析屏幕终点所在UI平面的Controller矩形。</param>
            public DistanceComparer(
                Vector3 endScreenPoint,
                Camera uiCamera,
                RectTransform rectTransform)
            {
                // Controller矩形有效时把屏幕终点投影到同一个UI世界平面。
                if (rectTransform &&
                    RectTransformUtility.ScreenPointToWorldPointInRectangle(
                        rectTransform,
                        endScreenPoint,
                        uiCamera,
                        out var endPointWorld))
                {
                    // 保存成功解析的正式世界坐标。
                    _endPointWorld = endPointWorld;
                    return;
                }

                // 异常非UI节点使用世界原点降级，排序保持确定且不会抛出空引用。
                _endPointWorld = Vector3.zero;
            }

            /// <summary>
            ///  比较两个流动网格到统一终点的世界空间距离。
            /// </summary>
            /// <param name="x">排序左侧候选组件。</param>
            /// <param name="y">排序右侧候选组件。</param>
            /// <returns>小于零表示x在前，等于零表示相同，大于零表示y在前。</returns>
            public int Compare(UIFlowMeshEffect x, UIFlowMeshEffect y)
            {
                // 两个候选都失效时视为相同。
                if (!x && !y) return 0;

                // 单独失效的x排在有效对象之后。
                if (!x) return 1;

                // 单独失效的y排在有效对象之后。
                if (!y) return -1;

                // 同一Unity对象不需要重复计算距离。
                if (ReferenceEquals(x, y)) return 0;

                // 两个候选都使用同一世界终点计算直线距离。
                var xDistance = Vector3.Distance(x.transform.position, _endPointWorld);
                var yDistance = Vector3.Distance(y.transform.position, _endPointWorld);

                // 优先按照实际距离从近到远排序。
                var distanceComparison = xDistance.CompareTo(yDistance);
                if (distanceComparison != 0) return distanceComparison;

                // 相同距离使用Unity实体编号稳定打破平局，List.Sort不会在帧间随机交换顺序。
                return x.GetEntityId().CompareTo(y.GetEntityId());
            }
        }
    }
}

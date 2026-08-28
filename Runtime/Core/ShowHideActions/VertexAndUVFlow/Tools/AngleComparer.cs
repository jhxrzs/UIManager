using System.Collections.Generic;
using UnityEngine;

namespace JH
{
    public partial class UIFlowGroupController
    {
        /// <summary>
        ///  按子物体相对终点的屏幕方向顺时针排序流动网格。
        /// </summary>
        private readonly struct AngleComparer : IComparer<UIFlowMeshEffect>
        {
            /// <summary>
            ///  当前统一主路径终点的屏幕坐标。
            /// </summary>
            private readonly Vector2 _endScreenPoint;

            /// <summary>
            ///  顺时针角度计算使用的屏幕空间起始方向。
            /// </summary>
            private readonly Vector2 _originVector;

            /// <summary>
            ///  当前Canvas进行世界坐标到屏幕坐标转换所需的相机。
            /// </summary>
            private readonly Camera _uiCamera;

            /// <summary>
            ///  创建一个只读的屏幕角度比较器。
            /// </summary>
            /// <param name="endScreenPoint">当前统一主路径终点的屏幕坐标。</param>
            /// <param name="originVector">顺时针角度计算使用的屏幕空间起始方向。</param>
            /// <param name="uiCamera">当前Canvas对应的相机；Overlay模式为空。</param>
            public AngleComparer(
                Vector2 endScreenPoint,
                Vector2 originVector,
                Camera uiCamera)
            {
                // 保存统一屏幕终点，全部候选使用相同方向原点。
                _endScreenPoint = endScreenPoint;

                // 保存用户配置的角度基准向量。
                _originVector = originVector;

                // 保存Canvas坐标转换需要的相机引用。
                _uiCamera = uiCamera;
            }

            /// <summary>
            ///  比较两个流动网格相对终点的顺时针角度。
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

                // 同一Unity对象不需要继续计算屏幕坐标。
                if (ReferenceEquals(x, y)) return 0;

                // 分别取得两个Graphic视觉节点的屏幕坐标。
                var xScreenPoint = RectTransformUtility.WorldToScreenPoint(
                    _uiCamera,
                    x.transform.position);
                var yScreenPoint = RectTransformUtility.WorldToScreenPoint(
                    _uiCamera,
                    y.transform.position);

                // 从统一终点指向候选位置，形成角度比较使用的单位方向。
                var xDirection = (xScreenPoint - _endScreenPoint).normalized;
                var yDirection = (yScreenPoint - _endScreenPoint).normalized;

                // 两个方向使用完全相同的顺时针零度基准。
                var xAngle = GetClockwiseAngle(xDirection);
                var yAngle = GetClockwiseAngle(yDirection);

                // 优先按照实际角度排序。
                var angleComparison = xAngle.CompareTo(yAngle);
                if (angleComparison != 0) return angleComparison;

                // 相同角度使用Unity实体编号稳定打破平局，List.Sort不会在帧间随机交换顺序。
                return x.GetEntityId().CompareTo(y.GetEntityId());
            }

            /// <summary>
            ///  计算指定方向相对配置基准的零到三百六十度顺时针角度。
            /// </summary>
            /// <param name="direction">从统一终点指向候选物体的单位方向。</param>
            /// <returns>零到三百六十度之间的顺时针角度。</returns>
            private float GetClockwiseAngle(Vector2 direction)
            {
                // SignedAngle返回负值时表示位于当前基准方向的顺时针一侧。
                var angle = Vector2.SignedAngle(_originVector, direction);

                // 把负角度平移到统一零到三百六十度范围。
                if (angle < 0f) angle += 360f;

                // 返回可直接参与升序比较的标准角度。
                return angle;
            }
        }
    }
}

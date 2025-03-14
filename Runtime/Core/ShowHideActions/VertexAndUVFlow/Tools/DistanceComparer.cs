using System.Collections.Generic;
using UnityEngine;

namespace JH
{
    public partial class VertexUVFlowController
    {
        /// <summary>
        ///   距离比较器
        /// </summary>
        private readonly struct DistanceComparer : IComparer<VertexUVFlow>
        {
            private readonly Vector3 _endPointWS;

            public DistanceComparer(Vector3 endScreenPoint, Camera uiCamera, RectTransform transform)
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    transform, endScreenPoint, uiCamera, out _endPointWS);
            }

            /// <summary>
            ///  比较器，距离越小，越靠前
            ///  返回值 《 0  : x 应该排在 y 前面
            ///  返回值 = 0  : x 和 y 相等
            ///  返回值 》 0  : x 应该排在 y 后面
            /// </summary>
            public int Compare(VertexUVFlow x, VertexUVFlow y)
            {
                // 处理null值（null值应排在最后）
                if (x == null && y == null) return 0;
                if (x == null) return 1;
                if (y == null) return -1;

                // 2. 相同引用直接返回
                if (ReferenceEquals(x, y)) return 0;

                // 3. 获取或计算距离
                float xDistance = Vector3.Distance(x.transform.position, _endPointWS);
                float yDistance = Vector3.Distance(y.transform.position, _endPointWS);

                // 4. 比较距离
                int compareResult = xDistance.CompareTo(yDistance);

                return compareResult;
            }
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace JH
{
    public partial class VertexUVFlowController
    {
        /// <summary>
        ///  角度比较器
        /// </summary>
        private readonly struct AngleComparer : IComparer<VertexUVFlow>
        {
            private readonly Vector2 _endScreenPoint;
            private readonly Vector2 _originVector;
            private readonly Camera _uiCamera;
            private readonly RectTransform _rectTransform;

            public AngleComparer(Vector2 endScreenPoint, Vector2 originVector, Camera uiCamera,
                RectTransform rectTransform)
            {
                _endScreenPoint = endScreenPoint;
                _originVector = originVector;
                _uiCamera = uiCamera;
                _rectTransform = rectTransform;
            }

            /// <summary>
            ///  比较器，距离越小，越靠前
            ///  返回值 《 0  : x 应该排在 y 前面
            ///  返回值 = 0  : x 和 y 相等
            ///  返回值 》 0  : x 应该排在 y 后面
            /// </summary>
            public int Compare(VertexUVFlow x, VertexUVFlow y)
            {
                // 1. null检查
                if (x == null && y == null) return 0;
                if (x == null) return 1;
                if (y == null) return -1;
                if (ReferenceEquals(x, y)) return 0;

                // 2. 计算屏幕坐标
                Vector2 xScreenPoint = RectTransformUtility.WorldToScreenPoint(_uiCamera, x.transform.position);
                Vector2 yScreenPoint = RectTransformUtility.WorldToScreenPoint(_uiCamera, y.transform.position);

                // 3. 计算相对于终点的向量
                Vector2 xDirection = (xScreenPoint - _endScreenPoint).normalized;
                Vector2 yDirection = (yScreenPoint - _endScreenPoint).normalized;

                // 4. 计算顺时针角度（0-360度）
                float xAngle = GetClockwiseAngle(xDirection);
                float yAngle = GetClockwiseAngle(yDirection);

                // 5. 比较角度
                return xAngle.CompareTo(yAngle);
            }

            /// <summary>
            /// 计算顺时针角度（0-360度）
            /// </summary>
            private float GetClockwiseAngle(Vector2 direction)
            {
                // 计算与右向量的夹角
                float angle = Vector2.SignedAngle(_originVector, direction);

                // 转换为顺时针0-360度
                if (angle < 0)
                {
                    angle += 360f;
                }

                return angle;
            }
        }
    }
}
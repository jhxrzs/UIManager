using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace JH
{
    /// <summary>
    ///  保存二维流动路径上一个采样点的位置、方向和曲率。
    /// </summary>
    [MovedFrom(true, sourceNamespace: "JH", sourceAssembly: "com.jh.uimanager.runtime",
        sourceClassName: "VertexFlowPose")]
    public readonly struct UIFlowPose
    {
        /// <summary>
        ///  创建一个不可变的二维路径姿态。
        /// </summary>
        /// <param name="position">路径上的本地位置。</param>
        /// <param name="tangent">路径前进方向的单位切线。</param>
        /// <param name="normal">切线逆时针旋转九十度后的单位法线。</param>
        /// <param name="curvature">当前采样点的绝对曲率。</param>
        public UIFlowPose(
            Vector2 position,
            Vector2 tangent,
            Vector2 normal,
            float curvature)
        {
            // 保存采样点在路径坐标空间中的位置。
            Position = position;
            // 保存路径前进方向的单位切线。
            Tangent = tangent;
            // 保存与切线垂直的单位法线。
            Normal = normal;
            // 保存用于限制拖尾宽度的绝对曲率。
            Curvature = curvature;
        }

        /// <summary>
        ///  路径上的本地位置。
        /// </summary>
        public Vector2 Position { get; }

        /// <summary>
        ///  路径前进方向的单位切线。
        /// </summary>
        public Vector2 Tangent { get; }

        /// <summary>
        ///  切线逆时针旋转九十度后的单位法线。
        /// </summary>
        public Vector2 Normal { get; }

        /// <summary>
        ///  当前采样点的绝对曲率。
        /// </summary>
        /// <remarks>
        ///  曲率单位是本地坐标单位的倒数，可以用于限制Ribbon宽度并避免法线偏移翻面。
        /// </remarks>
        public float Curvature { get; }
    }
}

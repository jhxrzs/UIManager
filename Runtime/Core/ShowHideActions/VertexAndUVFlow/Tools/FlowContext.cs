using UnityEngine;

namespace JH
{
    /// <summary>保存流动网格在一次重建期间使用的只读形变上下文。</summary>
    public partial class UIFlowMeshEffect
    {
        /// <summary>
        ///  保存一帧内全部顶点共享的贝塞尔漏斗参数。
        /// </summary>
        private readonly struct FlowContext
        {
            /// <summary>
            ///  创建不可变的流动路径上下文。
            /// </summary>
            /// <param name="sourceCenter">Graphic原始顶点边界的本地中心。</param>
            /// <param name="targetPoint">屏幕终点转换到Graphic本地空间后的收束点。</param>
            /// <param name="targetDirection">从原始中心指向收束点的标准化方向。</param>
            /// <param name="targetDistance">原始中心到收束点的弦长。</param>
            /// <param name="projectionRadius">原图形沿目标方向投影后的半径。</param>
            /// <param name="path">全部顶点共同使用的不可变S曲线路径。</param>
            /// <param name="formationOffset">当前子物体相对统一主路径起点的本地队形偏移。</param>
            /// <param name="configuration">当前帧使用的不可变网格形变参数。</param>
            /// <param name="progress">已经应用组件唯一时间缓动的当前帧进度。</param>
            /// <param name="useSharedFlowShader">当前材质是否从uv0.z读取独立流动进度。</param>
            public FlowContext(
                Vector2 sourceCenter,
                Vector2 targetPoint,
                Vector2 targetDirection,
                float targetDistance,
                float projectionRadius,
                UIFlowPath2D path,
                Vector2 formationOffset,
                UIFlowMeshConfiguration configuration,
                float progress,
                bool useSharedFlowShader)
            {
                // 保存全部顶点共同使用的原始中心。
                SourceCenter = sourceCenter;

                // 保存当前Graphic本地空间中的最终收束点。
                TargetPoint = targetPoint;

                // 保存从原始中心指向收束点的标准化方向。
                TargetDirection = targetDirection;

                // 保存原始中心到收束点的弦长。
                TargetDistance = targetDistance;

                // 保存原图形在目标方向上的投影半径。
                ProjectionRadius = projectionRadius;

                // 保存当前帧使用的不可变S曲线路径。
                Path = path;

                // 保存当前子物体相对统一主路径起点的初始队形偏移。
                FormationOffset = formationOffset;

                // 保存Controller统一参数或单组件降级参数。
                Configuration = configuration;

                // 保存已经应用唯一时间缓动的当前帧进度。
                Progress = progress;

                // 保存当前材质是否能够从基础UV通道读取独立流动进度。
                UseSharedFlowShader = useSharedFlowShader;
            }

            /// <summary>
            ///  原始图形中心。
            /// </summary>
            public Vector2 SourceCenter { get; }

            /// <summary>
            ///  当前Graphic本地空间中的收束点。
            /// </summary>
            public Vector2 TargetPoint { get; }

            /// <summary>
            ///  从源中心指向终点的单位方向。
            /// </summary>
            public Vector2 TargetDirection { get; }

            /// <summary>
            ///  源中心到终点的本地距离。
            /// </summary>
            public float TargetDistance { get; }

            /// <summary>
            ///  原图形沿目标方向投影后的半径。
            /// </summary>
            public float ProjectionRadius { get; }

            /// <summary>
            ///  当前漏斗与刚性主体、Ribbon共同使用的不可变S曲线路径。
            /// </summary>
            public UIFlowPath2D Path { get; }

            /// <summary>
            ///  当前子物体相对统一主路径起点的初始队形偏移。
            /// </summary>
            public Vector2 FormationOffset { get; }

            /// <summary>
            ///  当前帧全部顶点共同使用的不可变网格形变参数。
            /// </summary>
            public UIFlowMeshConfiguration Configuration { get; }

            /// <summary>
            ///  已经应用组件唯一时间曲线的本帧进度。
            /// </summary>
            public float Progress { get; }

            /// <summary>
            ///  当前材质是否使用uv0.z读取逐Graphic独立流动进度。
            /// </summary>
            public bool UseSharedFlowShader { get; }

            /// <summary>
            ///  当前材质不支持共享流动Shader时是否使用顶点Alpha降级。
            /// </summary>
            public bool UseVertexFade => !UseSharedFlowShader;
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;

namespace JH
{
    /// <summary>
    ///  沿<see cref="VertexFlowPath2D"/>生成具有柔和边缘的程序化UI Ribbon拖尾。
    /// </summary>
    /// <remarks>
    ///  Ribbon使用固定二十四个纵向站点和四条横向Alpha通道，不保存历史帧，动态目标变化时
    ///  会依据当前同一条路径解析式重建，因此不会产生逐帧托管内存分配。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VertexFlowRibbonGraphic : MaskableGraphic
    {
        /// <summary>
        ///  Ribbon沿路径使用的固定纵向站点数量。
        /// </summary>
        private const int RibbonStationCount = 24;

        /// <summary>
        ///  每个站点使用的固定横向软边通道数量。
        /// </summary>
        private const int RibbonLaneCount = 4;

        /// <summary>
        ///  弧长表使用的固定折线段数量。
        /// </summary>
        private const int ArcLengthSegmentCount = 48;

        /// <summary>
        ///  弧长表包含起点时需要的固定数组长度。
        /// </summary>
        private const int ArcLengthTableSize = ArcLengthSegmentCount + 1;

        /// <summary>
        ///  路径、宽度和透明度计算使用的最小有效值。
        /// </summary>
        private const float MinimumFlowValue = 0.0001f;

        /// <summary>
        ///  默认允许的最大拖尾本地长度。
        /// </summary>
        private const float DefaultMaximumTrailLength = 120f;

        /// <summary>
        ///  默认允许的拖尾长度占整条曲线弧长的比例。
        /// </summary>
        private const float DefaultMaximumTrailFraction = 0.18f;

        /// <summary>
        ///  默认拖尾半宽占源图形横截面半径的比例。
        /// </summary>
        private const float DefaultWidthRatio = 0.55f;

        /// <summary>
        ///  默认拖尾本地半宽上限。
        /// </summary>
        private const float DefaultMaximumHalfWidth = 20f;

        /// <summary>
        ///  默认拖尾整体不透明度。
        /// </summary>
        private const float DefaultOpacity = 0.32f;

        /// <summary>
        ///  拖尾最大长度，使用当前全屏Canvas的本地坐标单位。
        /// </summary>
        [SerializeField, Min(0f)]
        private float maximumTrailLength = DefaultMaximumTrailLength;

        /// <summary>
        ///  拖尾长度占当前S曲线近似总弧长的最大比例。
        /// </summary>
        [SerializeField, Range(0f, 0.5f)]
        private float maximumTrailFraction = DefaultMaximumTrailFraction;

        /// <summary>
        ///  拖尾半宽占调用方提供的源图形横截面半径的比例。
        /// </summary>
        [SerializeField, Range(0f, 1f)]
        private float widthRatio = DefaultWidthRatio;

        /// <summary>
        ///  拖尾在当前Canvas本地坐标中的最大半宽。
        /// </summary>
        [SerializeField, Min(0f)]
        private float maximumHalfWidth = DefaultMaximumHalfWidth;

        /// <summary>
        ///  拖尾在生命周期中点允许达到的整体不透明度。
        /// </summary>
        [SerializeField, Range(0f, 1f)]
        private float opacity = DefaultOpacity;

        /// <summary>
        ///  拖尾宽度从头部向尾端收束时使用的幂指数。
        /// </summary>
        [SerializeField, Range(0.1f, 4f)]
        private float widthPower = 0.7f;

        /// <summary>
        ///  拖尾透明度从头部向尾端消失时使用的幂指数。
        /// </summary>
        [SerializeField, Range(0.1f, 4f)]
        private float alphaPower = 1.6f;

        /// <summary>
        ///  两条不透明内部通道相对外轮廓半宽的位置。
        /// </summary>
        [SerializeField, Range(0f, 1f)]
        private float innerLaneRatio = 0.58f;

        /// <summary>
        ///  曲率半径用于限制Ribbon半宽时保留的安全比例。
        /// </summary>
        [SerializeField, Range(0.05f, 0.95f)]
        private float curvatureSafety = 0.45f;

        /// <summary>
        ///  当前需要渲染的不可变S曲线路径。
        /// </summary>
        [NonSerialized]
        private VertexFlowPath2D flowPath;

        /// <summary>
        ///  当前已经应用唯一时间缓动的路径进度。
        /// </summary>
        private float flowProgress;

        /// <summary>
        ///  源图形在路径横截面方向上的本地半径。
        /// </summary>
        private float sourceTransverseRadius;

        /// <summary>
        ///  当前拖尾在Graphic颜色以外使用的业务颜色。
        /// </summary>
        private Color flowColor = Color.white;

        /// <summary>
        ///  当前是否持有一组等待Canvas重建的有效流动数据。
        /// </summary>
        private bool hasFlow;

        /// <summary>
        ///  当前路径使用固定四十八段折线计算得到的近似总弧长。
        /// </summary>
        private float totalArcLength;

        /// <summary>
        ///  固定容量的累计弧长表，只在组件生命周期首次使用时创建一次。
        /// </summary>
        [NonSerialized]
        private float[] cumulativeArcLengths;

        /// <summary>
        ///  初始化不可交互的程序化Graphic和固定弧长缓存。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            // 转移动效仅负责视觉表现，禁止透明Ribbon阻挡任何UI射线。
            raycastTarget = false;

            // 固定数组只在生命周期入口分配一次，播放期间完整复用。
            EnsureArcLengthBuffer();
        }

        /// <summary>
        ///  组件重新启用时恢复固定缓存并请求一次正确网格。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            // 脚本重载可能清空非序列化数组，启用入口负责按固定容量恢复。
            EnsureArcLengthBuffer();

            // 池化对象重新显示时必须依据当前hasFlow状态重新提交网格。
            SetVerticesDirty();
        }

        /// <summary>
        ///  在编辑器阶段限制全部外观参数并保持Ribbon不参与交互。
        /// </summary>
        protected override void OnValidate()
        {
            base.OnValidate();

            // 全部序列化外观值统一限制在运行时公式可以稳定处理的范围内。
            maximumTrailLength = Mathf.Max(0f, maximumTrailLength);
            maximumTrailFraction = Mathf.Clamp(maximumTrailFraction, 0f, 0.5f);
            widthRatio = Mathf.Clamp01(widthRatio);
            maximumHalfWidth = Mathf.Max(0f, maximumHalfWidth);
            opacity = Mathf.Clamp01(opacity);
            widthPower = Mathf.Clamp(widthPower, 0.1f, 4f);
            alphaPower = Mathf.Clamp(alphaPower, 0.1f, 4f);
            innerLaneRatio = Mathf.Clamp01(innerLaneRatio);
            curvatureSafety = Mathf.Clamp(curvatureSafety, 0.05f, 0.95f);
            raycastTarget = false;

            // 编辑器修改参数后让Canvas在下一次重建时立即反映新的拖尾轮廓。
            SetVerticesDirty();
        }

        /// <summary>
        ///  在运行时一次性配置Ribbon的主要长度、宽度和透明度风格。
        /// </summary>
        /// <param name="newMaximumTrailLength">拖尾最大本地长度。</param>
        /// <param name="newMaximumTrailFraction">拖尾最大长度占路径弧长的比例。</param>
        /// <param name="newWidthRatio">拖尾半宽占源横截面半径的比例。</param>
        /// <param name="newMaximumHalfWidth">拖尾本地半宽上限。</param>
        /// <param name="newOpacity">拖尾生命周期中点的整体不透明度。</param>
        public void ConfigureStyle(
            float newMaximumTrailLength,
            float newMaximumTrailFraction,
            float newWidthRatio,
            float newMaximumHalfWidth,
            float newOpacity)
        {
            // 公共入口使用与OnValidate相同的边界，动态创建组件和Prefab拥有一致语义。
            maximumTrailLength = Mathf.Max(0f, newMaximumTrailLength);
            maximumTrailFraction = Mathf.Clamp(newMaximumTrailFraction, 0f, 0.5f);
            widthRatio = Mathf.Clamp01(newWidthRatio);
            maximumHalfWidth = Mathf.Max(0f, newMaximumHalfWidth);
            opacity = Mathf.Clamp01(newOpacity);

            // 风格变化只影响顶点位置和颜色，不需要创建或修改任何材质实例。
            SetVerticesDirty();
        }

        /// <summary>
        ///  提交本帧刚性主体共用的路径和已经缓动的进度。
        /// </summary>
        /// <param name="path">刚性主体与Ribbon共同使用的本地S曲线路径。</param>
        /// <param name="easedProgress">已经应用唯一时间缓动的零到一路径进度。</param>
        /// <param name="sourceRadius">源图形沿路径横截面方向的本地半径。</param>
        /// <param name="color">本次拖尾需要使用的业务颜色。</param>
        public void SetFlow(
            in VertexFlowPath2D path,
            float easedProgress,
            float sourceRadius,
            Color color)
        {
            // 脚本重载或动态AddComponent后首次调用时恢复固定容量缓存。
            EnsureArcLengthBuffer();

            // 全部流动状态使用值类型复制，不持有调用方的临时对象或委托。
            flowPath = path;
            flowProgress = Mathf.Clamp01(easedProgress);
            sourceTransverseRadius = Mathf.Max(0f, sourceRadius);
            flowColor = color;
            hasFlow = true;

            // 动态目标每帧可能改变控制点，累计弧长表必须与本帧同一条路径一起重建。
            totalArcLength = flowPath.BuildArcLengthTable(cumulativeArcLengths);

            // 交给UGUI标准重建流程，避免手工SetMesh破坏Mask或Canvas批处理。
            SetVerticesDirty();
        }

        /// <summary>
        ///  清除当前流动状态并在下一次Canvas重建时移除全部Ribbon顶点。
        /// </summary>
        public void ClearFlow()
        {
            // 清空所有权后迟到的Canvas重建只能得到空网格。
            hasFlow = false;
            flowProgress = 0f;
            totalArcLength = 0f;

            // 已经分配的弧长数组继续保留给下一次池化播放复用。
            SetVerticesDirty();
        }

        /// <summary>
        ///  使用固定站点和横向通道生成当前帧的Ribbon连续网格。
        /// </summary>
        /// <param name="vertexHelper">UGUI为当前Graphic提供的网格写入器。</param>
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            // 每次重建都从空拓扑开始，禁止在上一帧动态目标的网格上累计误差。
            vertexHelper.Clear();

            // 首尾帧的Ribbon宽度、长度和Alpha都为零，直接保持空网格避免退化亮点。
            if (!hasFlow ||
                flowProgress <= MinimumFlowValue ||
                flowProgress >= 1f - MinimumFlowValue ||
                totalArcLength <= MinimumFlowValue ||
                sourceTransverseRadius <= MinimumFlowValue)
                return;

            // 四次对称生命周期包络在零和一处同时拥有零值与零一阶导数。
            var lifecycle = EvaluateLifecycle(flowProgress);
            if (lifecycle <= MinimumFlowValue) return;

            // 头部弧长严格对应刚性主体当前路径参数，Ribbon只向已经经过的路径后方展开。
            var headArcLength = flowPath.GetLengthAtParameter(
                flowProgress,
                cumulativeArcLengths);

            // 拖尾长度同时受绝对值、路径比例、生命周期和已经走过的距离约束。
            var configuredTrailLength = Mathf.Min(
                maximumTrailLength,
                totalArcLength * maximumTrailFraction);
            var activeTrailLength = Mathf.Min(
                configuredTrailLength * lifecycle,
                headArcLength);
            if (activeTrailLength <= MinimumFlowValue) return;

            // 当前帧基础宽度使用统一生命周期收放，源图形本身始终由独立刚性Image保持完整。
            var baseHalfWidth = Mathf.Min(
                                    sourceTransverseRadius * widthRatio,
                                    maximumHalfWidth) *
                                lifecycle;
            if (baseHalfWidth <= MinimumFlowValue) return;

            // Graphic颜色与业务颜色只计算一次，随后每个通道仅修改自身Alpha。
            var combinedColor = color * flowColor;
            var previousNormal = Vector2.zero;
            var hasPreviousNormal = false;

            // 先按头到尾顺序写入全部站点的四条横向软边通道。
            for (var stationIndex = 0;
                 stationIndex < RibbonStationCount;
                 stationIndex++)
            {
                var stationRatio = stationIndex / (float)(RibbonStationCount - 1);

                // 使用累计弧长反查参数，弯曲段和近直线段都保持稳定的空间采样密度。
                var stationArcLength = headArcLength -
                                       stationRatio * activeTrailLength;
                var stationProgress = flowPath.GetParameterAtLength(
                    stationArcLength,
                    cumulativeArcLengths);
                var pose = flowPath.Evaluate(stationProgress);
                var stationNormal = pose.Normal;

                // 理论连续法线在数值退化点仍可能反号，逐站校正保证Ribbon横截面不翻转。
                if (hasPreviousNormal && Vector2.Dot(stationNormal, previousNormal) < 0f)
                    stationNormal = -stationNormal;
                previousNormal = stationNormal;
                hasPreviousNormal = true;

                // 标准SmoothStep轮廓让宽度在尾端以零斜率收束，再由幂指数控制饱满程度。
                var smoothStationRatio = stationRatio * stationRatio *
                                         (3f - 2f * stationRatio);
                var widthProfile = Mathf.Pow(
                    Mathf.Max(0f, 1f - smoothStationRatio),
                    widthPower);
                var stationHalfWidth = baseHalfWidth * widthProfile;

                // 法线偏移必须小于局部曲率半径，安全系数防止两侧边缘形成尖点或翻面。
                if (pose.Curvature > MinimumFlowValue)
                    stationHalfWidth = Mathf.Min(
                        stationHalfWidth,
                        curvatureSafety / pose.Curvature);

                // 透明度沿头到尾使用独立幂包络，尾端在几何收束前已经完全透明。
                var alphaProfile = Mathf.Pow(
                    Mathf.Max(0f, 1f - stationRatio),
                    alphaPower);
                var stationAlpha = combinedColor.a * opacity *
                                   lifecycle * alphaProfile;

                // 四条横向通道分别写入外透明边、两条实体内边和另一侧透明边。
                for (var laneIndex = 0; laneIndex < RibbonLaneCount; laneIndex++)
                {
                    var laneOffset = GetLaneOffset(laneIndex);
                    var laneAlpha = GetLaneAlpha(laneIndex);
                    var lanePosition = pose.Position +
                                       stationNormal *
                                       (stationHalfWidth * laneOffset);

                    var vertex = UIVertex.simpleVert;
                    vertex.position = new Vector3(lanePosition.x, lanePosition.y, 0f);

                    // RGB保持整条Ribbon一致，Alpha由纵向包络与横向软边共同决定。
                    var vertexColor = combinedColor;
                    vertexColor.a = stationAlpha * laneAlpha;
                    vertex.color = vertexColor;

                    // UV保存头尾比例和横向通道比例，未来共享Shader可以直接复用而无需改网格API。
                    vertex.uv0 = new Vector4(
                        stationRatio,
                        laneIndex / (float)(RibbonLaneCount - 1),
                        0f,
                        0f);
                    vertexHelper.AddVert(vertex);
                }
            }

            // 相邻站点之间的三条横带各使用两个相同绕序三角形，避免出现裂缝或棋盘翻面。
            for (var stationIndex = 0;
                 stationIndex < RibbonStationCount - 1;
                 stationIndex++)
            {
                for (var laneIndex = 0;
                     laneIndex < RibbonLaneCount - 1;
                     laneIndex++)
                {
                    var currentLeft = stationIndex * RibbonLaneCount + laneIndex;
                    var currentRight = currentLeft + 1;
                    var nextLeft = currentLeft + RibbonLaneCount;
                    var nextRight = nextLeft + 1;

                    vertexHelper.AddTriangle(currentLeft, nextLeft, nextRight);
                    vertexHelper.AddTriangle(currentLeft, nextRight, currentRight);
                }
            }
        }

        /// <summary>
        ///  返回指定横向通道相对于当前Ribbon半宽的位置比例。
        /// </summary>
        private float GetLaneOffset(int laneIndex)
        {
            return laneIndex switch
            {
                0 => 1f,
                1 => innerLaneRatio,
                2 => -innerLaneRatio,
                _ => -1f
            };
        }

        /// <summary>
        ///  返回指定横向通道用于生成线性软边的Alpha比例。
        /// </summary>
        private static float GetLaneAlpha(int laneIndex)
        {
            // 两条外轮廓完全透明，两条内部通道完整保留当前站点Alpha。
            return laneIndex == 0 || laneIndex == RibbonLaneCount - 1 ? 0f : 1f;
        }

        /// <summary>
        ///  计算首尾值和一阶导都为零、动画中点为一的对称生命周期包络。
        /// </summary>
        private static float EvaluateLifecycle(float progress)
        {
            var clampedProgress = Mathf.Clamp01(progress);
            var inverseProgress = 1f - clampedProgress;
            return 16f * clampedProgress * clampedProgress *
                   inverseProgress * inverseProgress;
        }

        /// <summary>
        ///  保证非序列化累计弧长数组使用唯一固定容量并只创建一次。
        /// </summary>
        private void EnsureArcLengthBuffer()
        {
            if (cumulativeArcLengths != null &&
                cumulativeArcLengths.Length == ArcLengthTableSize)
                return;

            cumulativeArcLengths = new float[ArcLengthTableSize];
        }
    }
}

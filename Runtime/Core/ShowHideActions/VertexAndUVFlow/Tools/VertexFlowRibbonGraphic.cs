using System;
using UnityEngine;
using UnityEngine.UI;

namespace JH
{
    /// <summary>
    ///  沿<see cref="UIFlowPath2D"/>生成具有柔和边缘的程序化UI Ribbon拖尾。
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
        private UIFlowPath2D flowPath;

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
            // 先完成 MaskableGraphic 自身的渲染与遮罩初始化。
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
            // 恢复 Graphic 的启用流程，再恢复 Ribbon 缓冲区与标记。
            base.OnEnable();

            // 脚本重载可能清空非序列化数组，启用入口负责按固定容量恢复。
            EnsureArcLengthBuffer();

            // 池化对象重新显示时必须依据当前hasFlow状态重新提交网格。
            SetVerticesDirty();
        }

#if UNITY_EDITOR
        /// <summary>
        ///  在编辑器阶段限制全部外观参数并保持Ribbon不参与交互。
        /// </summary>
        protected override void OnValidate()
        {
            // 保留基类编辑器校验和网格更新行为。
            base.OnValidate();

            // 全部序列化外观值统一限制在运行时公式可以稳定处理的范围内。
            maximumTrailLength = Mathf.Max(0f, maximumTrailLength);
            // 限制拖尾占整条路径的比例，避免尾部覆盖过长路径。
            maximumTrailFraction = Mathf.Clamp(maximumTrailFraction, 0f, 0.5f);
            // 把相对源图形的宽度比例限制为零到一。
            widthRatio = Mathf.Clamp01(widthRatio);
            // 拖尾半宽绝对上限不能为负。
            maximumHalfWidth = Mathf.Max(0f, maximumHalfWidth);
            // 将额外透明度限制到有效范围。
            opacity = Mathf.Clamp01(opacity);
            // 限制沿拖尾的宽度衰减指数，避免极端尖锐截面。
            widthPower = Mathf.Clamp(widthPower, 0.1f, 4f);
            // 限制沿拖尾的透明度衰减指数。
            alphaPower = Mathf.Clamp(alphaPower, 0.1f, 4f);
            // 内部软边通道必须位于半宽范围内。
            innerLaneRatio = Mathf.Clamp01(innerLaneRatio);
            // 曲率安全系数保持正值且小于一，避免法线偏移翻面。
            curvatureSafety = Mathf.Clamp(curvatureSafety, 0.05f, 0.95f);
            // 拖尾仅用于表现，不阻挡 UI 射线交互。
            raycastTarget = false;

            // 编辑器修改参数后让Canvas在下一次重建时立即反映新的拖尾轮廓。
            SetVerticesDirty();
        }
#endif

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
            // 按同一安全范围保存新的相对拖尾长度上限。
            maximumTrailFraction = Mathf.Clamp(newMaximumTrailFraction, 0f, 0.5f);
            // 保存规范化后的宽度比例。
            widthRatio = Mathf.Clamp01(newWidthRatio);
            // 保存非负的绝对半宽上限。
            maximumHalfWidth = Mathf.Max(0f, newMaximumHalfWidth);
            // 保存规范化后的透明度系数。
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
            in UIFlowPath2D path,
            float easedProgress,
            float sourceRadius,
            Color color)
        {
            // 脚本重载或动态AddComponent后首次调用时恢复固定容量缓存。
            EnsureArcLengthBuffer();

            // 全部流动状态使用值类型复制，不持有调用方的临时对象或委托。
            flowPath = path;
            // 限制已缓动的进度，避免沿弧长采样越过路径端点。
            flowProgress = Mathf.Clamp01(easedProgress);
            // 源图形横向半径不能为负。
            sourceTransverseRadius = Mathf.Max(0f, sourceRadius);
            // 缓存主体颜色，让拖尾与来源保持一致。
            flowColor = color;
            // 标记当前存在可供网格生成使用的流动状态。
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
            // 清除旧动画进度，避免下次显示沿用末帧。
            flowProgress = 0f;
            // 清除旧路径弧长缓存对应的有效总长。
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
                // 无有效流动、进度或弧长时保持空网格。
                return;

            // 四次对称生命周期包络在零和一处同时拥有零值与零一阶导数。
            var lifecycle = EvaluateLifecycle(flowProgress);
            // 首尾包络接近零时省略几乎不可见的拖尾。
            if (lifecycle <= MinimumFlowValue) return;

            // 头部弧长严格对应刚性主体当前路径参数，Ribbon只向已经经过的路径后方展开。
            var headArcLength = flowPath.GetLengthAtParameter(
                flowProgress,
                cumulativeArcLengths);

            // 拖尾长度同时受绝对值、路径比例、生命周期和已经走过的距离约束。
            var configuredTrailLength = Mathf.Min(
                maximumTrailLength,
                totalArcLength * maximumTrailFraction);
            // 在绝对长度、路径比例和已前进距离之间取有效拖尾长度。
            var activeTrailLength = Mathf.Min(
                configuredTrailLength * lifecycle,
                headArcLength);
            // 有效长度接近零时无法生成有面积的 Ribbon。
            if (activeTrailLength <= MinimumFlowValue) return;

            // 当前帧基础宽度使用统一生命周期收放，源图形本身始终由独立刚性Image保持完整。
            var baseHalfWidth = Mathf.Min(
                                    sourceTransverseRadius * widthRatio,
                                    maximumHalfWidth) *
                                lifecycle;
            // 基础半宽接近零时无需构建不可见三角形。
            if (baseHalfWidth <= MinimumFlowValue) return;

            // Graphic颜色与业务颜色只计算一次，随后每个通道仅修改自身Alpha。
            var combinedColor = color * flowColor;
            // 初始化相邻站点法线连续性检查的缓存。
            var previousNormal = Vector2.zero;
            // 首站点没有前一法线，不执行方向校正。
            var hasPreviousNormal = false;

            // 先按头到尾顺序写入全部站点的四条横向软边通道。
            for (var stationIndex = 0;
                 stationIndex < RibbonStationCount;
                 stationIndex++)
            {
                // 把固定站点索引映射为从头部到尾部的比例。
                var stationRatio = stationIndex / (float)(RibbonStationCount - 1);

                // 使用累计弧长反查参数，弯曲段和近直线段都保持稳定的空间采样密度。
                var stationArcLength = headArcLength -
                                       stationRatio * activeTrailLength;
                // 通过累计弧长表把当前站点距离反查为路径参数。
                var stationProgress = flowPath.GetParameterAtLength(
                    stationArcLength,
                    cumulativeArcLengths);
                // 按反查参数采样位置、法线与曲率。
                var pose = flowPath.Evaluate(stationProgress);
                // 以路径采样法线作为当前横截面的初始方向。
                var stationNormal = pose.Normal;

                // 理论连续法线在数值退化点仍可能反号，逐站校正保证Ribbon横截面不翻转。
                if (hasPreviousNormal && Vector2.Dot(stationNormal, previousNormal) < 0f)
                    // 相邻法线反向时翻转当前方向，避免横截面扭转。
                    stationNormal = -stationNormal;
                // 保存已经校正的法线供下一站点比较。
                previousNormal = stationNormal;
                // 后续站点可以开始使用法线连续性检查。
                hasPreviousNormal = true;

                // 标准SmoothStep轮廓让宽度在尾端以零斜率收束，再由幂指数控制饱满程度。
                var smoothStationRatio = stationRatio * stationRatio *
                                         (3f - 2f * stationRatio);
                // 根据站点比例计算从主体头部逐渐收束到尾部的宽度轮廓。
                var widthProfile = Mathf.Pow(
                    Mathf.Max(0f, 1f - smoothStationRatio),
                    widthPower);
                // 将宽度轮廓作用于当前基础半宽。
                var stationHalfWidth = baseHalfWidth * widthProfile;

                // 法线偏移必须小于局部曲率半径，安全系数防止两侧边缘形成尖点或翻面。
                if (pose.Curvature > MinimumFlowValue)
                    // 按局部曲率限制站点半宽，避免内侧法线偏移相交。
                    stationHalfWidth = Mathf.Min(
                        stationHalfWidth,
                        curvatureSafety / pose.Curvature);

                // 透明度沿头到尾使用独立幂包络，尾端在几何收束前已经完全透明。
                var alphaProfile = Mathf.Pow(
                    Mathf.Max(0f, 1f - stationRatio),
                    alphaPower);
                // 叠加源颜色、样式透明度、生命周期与纵向尾部衰减。
                var stationAlpha = combinedColor.a * opacity *
                                   lifecycle * alphaProfile;

                // 四条横向通道分别写入外透明边、两条实体内边和另一侧透明边。
                for (var laneIndex = 0; laneIndex < RibbonLaneCount; laneIndex++)
                {
                    // 取得当前横向通道相对于半宽的偏移比例。
                    var laneOffset = GetLaneOffset(laneIndex);
                    // 取得当前通道用于线性软边的透明度比例。
                    var laneAlpha = GetLaneAlpha(laneIndex);
                    // 沿站点法线偏移，得到该通道的二维顶点位置。
                    var lanePosition = pose.Position +
                                       stationNormal *
                                       (stationHalfWidth * laneOffset);

                    // 从 Unity 默认 UI 顶点初始化完整通道。
                    var vertex = UIVertex.simpleVert;
                    // 将 Ribbon 平面位置写入本地坐标，深度保持为零。
                    vertex.position = new Vector3(lanePosition.x, lanePosition.y, 0f);

                    // RGB保持整条Ribbon一致，Alpha由纵向包络与横向软边共同决定。
                    var vertexColor = combinedColor;
                    // 把站点透明度乘以通道软边比例。
                    vertexColor.a = stationAlpha * laneAlpha;
                    // 写入保留 RGB、已衰减 Alpha 的顶点颜色。
                    vertex.color = vertexColor;

                    // UV保存头尾比例和横向通道比例，未来共享Shader可以直接复用而无需改网格API。
                    vertex.uv0 = new Vector4(
                        stationRatio,
                        laneIndex / (float)(RibbonLaneCount - 1),
                        0f,
                        0f);
                    // 追加当前站点通道顶点供后续拓扑连接。
                    vertexHelper.AddVert(vertex);
                }
            }

            // 相邻站点之间的三条横带各使用两个相同绕序三角形，避免出现裂缝或棋盘翻面。
            for (var stationIndex = 0;
                 stationIndex < RibbonStationCount - 1;
                 stationIndex++)
            {
                // 逐一连接相邻横向通道形成细分条带。
                for (var laneIndex = 0;
                     laneIndex < RibbonLaneCount - 1;
                     laneIndex++)
                {
                    // 计算当前站点左侧通道顶点索引。
                    var currentLeft = stationIndex * RibbonLaneCount + laneIndex;
                    // 当前站点右侧顶点紧邻左侧通道。
                    var currentRight = currentLeft + 1;
                    // 下一站点左侧顶点与当前站点相隔一个通道组。
                    var nextLeft = currentLeft + RibbonLaneCount;
                    // 下一站点右侧顶点紧邻其左侧通道。
                    var nextRight = nextLeft + 1;

                    // 添加条带单元的第一个三角形。
                    vertexHelper.AddTriangle(currentLeft, nextLeft, nextRight);
                    // 添加条带单元的第二个三角形，保持连续面拓扑。
                    vertexHelper.AddTriangle(currentLeft, nextRight, currentRight);
                }
            }
        }

        /// <summary>
        ///  返回指定横向通道相对于当前Ribbon半宽的位置比例。
        /// </summary>
        /// <param name="laneIndex">从零开始的 Ribbon 横向通道索引。</param>
        /// <returns>相对站点半宽的有符号横向偏移比例。</returns>
        private float GetLaneOffset(int laneIndex)
        {
            // 外侧通道位于正负半宽，内部通道按软边比例向内收缩。
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
        /// <param name="laneIndex">从零开始的 Ribbon 横向通道索引。</param>
        /// <returns>外侧通道返回零，内部通道返回一。</returns>
        private static float GetLaneAlpha(int laneIndex)
        {
            // 两条外轮廓完全透明，两条内部通道完整保留当前站点Alpha。
            return laneIndex == 0 || laneIndex == RibbonLaneCount - 1 ? 0f : 1f;
        }

        /// <summary>
        ///  计算首尾值和一阶导都为零、动画中点为一的对称生命周期包络。
        /// </summary>
        /// <param name="progress">将在求包络前限制为零到一的生命周期进度。</param>
        /// <returns>首尾为零、中点为一的对称透明度包络。</returns>
        private static float EvaluateLifecycle(float progress)
        {
            // 先把生命周期进度限制到闭区间。
            var clampedProgress = Mathf.Clamp01(progress);
            // 计算终点侧的对称时间补量。
            var inverseProgress = 1f - clampedProgress;
            // 对称四次包络在首尾为零、中点为一，且端点速度平滑。
            return 16f * clampedProgress * clampedProgress *
                   inverseProgress * inverseProgress;
        }

        /// <summary>
        ///  保证非序列化累计弧长数组使用唯一固定容量并只创建一次。
        /// </summary>
        private void EnsureArcLengthBuffer()
        {
            // 已有缓冲区容量正确时直接复用。
            if (cumulativeArcLengths != null &&
                cumulativeArcLengths.Length == ArcLengthTableSize)
                // 固定容量缓冲区已经准备好，无需再次分配。
                return;

            // 仅容量缺失或不匹配时创建新的累计弧长表。
            cumulativeArcLengths = new float[ArcLengthTableSize];
        }
    }
}

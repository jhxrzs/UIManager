using UnityEngine;

namespace JH
{
    /// <summary>
    ///  保存单个UI流动网格在一帧内使用的不可变形变参数。
    /// </summary>
    /// <remarks>
    ///  Controller创建统一配置后传递给全部子物体；脱离Controller的组件则从自身序列化字段创建配置。
    /// </remarks>
    internal readonly struct UIFlowMeshConfiguration
    {
        /// <summary>
        ///  普通Image默认的横向细分段数。
        /// </summary>
        internal const int DefaultHorizontalSegments = 24;

        /// <summary>
        ///  普通Image默认的纵向细分段数。
        /// </summary>
        internal const int DefaultVerticalSegments = 16;

        /// <summary>
        ///  网格至少保留的单轴细分段数。
        /// </summary>
        internal const int MinimumGridSegments = 4;

        /// <summary>
        ///  防止误配置产生过大Canvas网格的单轴上限。
        /// </summary>
        internal const int MaximumGridSegments = 48;

        /// <summary>
        ///  S曲线控制点沿起终点方向所在的默认比例。
        /// </summary>
        internal const float DefaultCurveControlDistanceRatio = 0.3f;

        /// <summary>
        ///  S曲线控制点沿弦方向允许使用的最小比例。
        /// </summary>
        internal const float MinimumCurveControlDistanceRatio = 0f;

        /// <summary>
        ///  S曲线控制点沿弦方向允许使用的最大比例。
        /// </summary>
        internal const float MaximumCurveControlDistanceRatio = 0.499f;

        /// <summary>
        ///  S曲线控制点垂直偏移占路径长度的安全上限。
        /// </summary>
        internal const float MaximumCurveBendRatio = UIFlowPath2D.DefaultMaximumBendRatio;

        /// <summary>
        ///  创建一份经过边界保护的不可变网格形变配置。
        /// </summary>
        /// <param name="horizontalSegments">横向最多生成的细分段数。</param>
        /// <param name="verticalSegments">纵向最多生成的细分段数。</param>
        /// <param name="flowDelay">背离终点的顶点相对前沿顶点的空间相位落后强度。</param>
        /// <param name="curveControlDistanceRatio">控制点沿起终点方向所在的比例。</param>
        /// <param name="curveBend">控制点垂直偏移占路径长度的比例。</param>
        /// <param name="maximumCurveBend">控制点垂直偏移的本地坐标绝对上限。</param>
        /// <param name="funnelPower">横截面向终点收束时使用的幂指数。</param>
        /// <param name="fadeStart">顶点透明度开始消失的曲线进度。</param>
        /// <param name="formationCollapsePower">子物体相对整体中心的队形偏移收束指数。</param>
        internal UIFlowMeshConfiguration(
            int horizontalSegments,
            int verticalSegments,
            float flowDelay,
            float curveControlDistanceRatio,
            float curveBend,
            float maximumCurveBend,
            float funnelPower,
            float fadeStart,
            float formationCollapsePower)
        {
            // 细分数量始终位于安全区间，禁止运行时配置生成过密或退化网格。
            HorizontalSegments = Mathf.Clamp(
                horizontalSegments,
                MinimumGridSegments,
                MaximumGridSegments);

            // 纵向细分使用与横向一致的安全上下限。
            VerticalSegments = Mathf.Clamp(
                verticalSegments,
                MinimumGridSegments,
                MaximumGridSegments);

            // 顶点相位落后保持在能够连续收束的经验范围内。
            FlowDelay = Mathf.Clamp(flowDelay, 0f, 0.75f);

            // 控制点不能越过路径中点，否则曲线可能沿弦方向回钩。
            CurveControlDistanceRatio = Mathf.Clamp(
                curveControlDistanceRatio,
                MinimumCurveControlDistanceRatio,
                MaximumCurveControlDistanceRatio);

            // 弯曲比例继续服从路径算法的相对安全上限。
            CurveBend = Mathf.Clamp(curveBend, 0f, MaximumCurveBendRatio);

            // 绝对弯曲上限不接受负数，零表示主动关闭弯曲。
            MaximumCurveBend = Mathf.Max(0f, maximumCurveBend);

            // 漏斗指数限制在已有视觉算法能够稳定工作的区间。
            FunnelPower = Mathf.Clamp(funnelPower, 0.2f, 2f);

            // 淡出起点必须早于最终帧，给透明度留下非零过渡区间。
            FadeStart = Mathf.Clamp(fadeStart, 0f, 0.99f);

            // 队形指数必须为正，避免零指数令终点前始终保留完整偏移。
            FormationCollapsePower = Mathf.Clamp(formationCollapsePower, 0.2f, 4f);
        }

        /// <summary>
        ///  横向最多生成的细分段数。
        /// </summary>
        internal int HorizontalSegments { get; }

        /// <summary>
        ///  纵向最多生成的细分段数。
        /// </summary>
        internal int VerticalSegments { get; }

        /// <summary>
        ///  背离终点的顶点相对前沿顶点的空间相位落后强度。
        /// </summary>
        internal float FlowDelay { get; }

        /// <summary>
        ///  S曲线控制点沿起终点方向所在的比例。
        /// </summary>
        internal float CurveControlDistanceRatio { get; }

        /// <summary>
        ///  S曲线控制点垂直偏移占路径长度的比例。
        /// </summary>
        internal float CurveBend { get; }

        /// <summary>
        ///  S曲线控制点垂直偏移的本地坐标绝对上限。
        /// </summary>
        internal float MaximumCurveBend { get; }

        /// <summary>
        ///  横截面向终点收束时使用的幂指数。
        /// </summary>
        internal float FunnelPower { get; }

        /// <summary>
        ///  顶点透明度开始消失的曲线进度。
        /// </summary>
        internal float FadeStart { get; }

        /// <summary>
        ///  子物体相对整体中心的队形偏移收束指数。
        /// </summary>
        internal float FormationCollapsePower { get; }
    }
}

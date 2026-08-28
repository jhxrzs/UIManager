using System;
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
            Position = position;
            Tangent = tangent;
            Normal = normal;
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

    /// <summary>
    ///  使用对称三次贝塞尔控制点描述一条可逆二维S曲线。
    /// </summary>
    /// <remarks>
    ///  对同一组端点反向创建路径时，两个控制点会严格交换，因此反向路径满足
    ///  <c>Reverse(t) == Forward(1 - t)</c>。
    /// </remarks>
    [MovedFrom(true, sourceNamespace: "JH", sourceAssembly: "com.jh.uimanager.runtime",
        sourceClassName: "VertexFlowPath2D")]
    public readonly struct UIFlowPath2D
    {
        /// <summary>
        ///  路径和导数计算使用的最小有效长度。
        /// </summary>
        private const float MinimumPathLength = 0.0001f;

        /// <summary>
        ///  防止控制点越过路径中点并产生纵向回钩的最大比例。
        /// </summary>
        private const float MaximumControlDistanceRatio = 0.499f;

        /// <summary>
        ///  默认允许的控制点垂直偏移占路径长度的最大比例。
        /// </summary>
        public const float DefaultMaximumBendRatio = 0.28f;

        /// <summary>
        ///  创建一条已经确定四个控制点的二维三次贝塞尔路径。
        /// </summary>
        private UIFlowPath2D(
            Vector2 start,
            Vector2 firstControlPoint,
            Vector2 secondControlPoint,
            Vector2 end,
            Vector2 chordDirection,
            float chordLength)
        {
            Start = start;
            FirstControlPoint = firstControlPoint;
            SecondControlPoint = secondControlPoint;
            End = end;
            ChordDirection = chordDirection;
            ChordLength = chordLength;
        }

        /// <summary>
        ///  路径起点。
        /// </summary>
        public Vector2 Start { get; }

        /// <summary>
        ///  三次贝塞尔路径的第一个控制点。
        /// </summary>
        public Vector2 FirstControlPoint { get; }

        /// <summary>
        ///  三次贝塞尔路径的第二个控制点。
        /// </summary>
        public Vector2 SecondControlPoint { get; }

        /// <summary>
        ///  路径终点。
        /// </summary>
        public Vector2 End { get; }

        /// <summary>
        ///  从起点指向终点的单位弦方向。
        /// </summary>
        public Vector2 ChordDirection { get; }

        /// <summary>
        ///  起点到终点之间的直线距离。
        /// </summary>
        public float ChordLength { get; }

        /// <summary>
        ///  当前路径的起终点是否已经退化到同一个位置。
        /// </summary>
        public bool IsDegenerate => ChordLength <= MinimumPathLength;

        /// <summary>
        ///  使用位于弦线两侧的对称控制点创建一条可逆S曲线。
        /// </summary>
        /// <param name="start">路径起点。</param>
        /// <param name="end">路径终点。</param>
        /// <param name="controlDistanceRatio">控制点沿起终点方向所在的比例。</param>
        /// <param name="bendRatio">控制点垂直偏移占起终点距离的比例。</param>
        /// <param name="maximumBend">控制点垂直偏移的本地坐标绝对上限。</param>
        /// <param name="maximumBendRatio">控制点垂直偏移占路径长度的安全上限。</param>
        /// <returns>可以正向或反向采样的不可变S曲线路径。</returns>
        public static UIFlowPath2D CreateReversibleS(
            Vector2 start,
            Vector2 end,
            float controlDistanceRatio,
            float bendRatio,
            float maximumBend,
            float maximumBendRatio = DefaultMaximumBendRatio)
        {
            // 起终点差值同时定义弦方向、弦长度和全部控制点的纵向基准。
            var chord = end - start;
            var chordLength = chord.magnitude;

            // 退化路径使用稳定的默认方向，禁止归一化零向量产生NaN。
            var chordDirection = chordLength > MinimumPathLength
                ? chord / chordLength
                : Vector2.right;

            // 控制点必须留在各自半段内，才能保证路径沿弦方向始终单调前进。
            var safeControlRatio = Mathf.Clamp(
                controlDistanceRatio,
                0f,
                MaximumControlDistanceRatio);

            // 直接使用弦方向的左法线；交换端点时该法线自然反向，禁止额外固定屏幕朝向。
            var chordNormal = new Vector2(-chordDirection.y, chordDirection.x);

            // 弯曲量同时受配置比例、绝对上限和路径相对上限约束。
            var ratioBend = chordLength * Mathf.Max(0f, bendRatio);
            var absoluteBend = Mathf.Max(0f, maximumBend);
            var safeRatioBend = chordLength * Mathf.Max(0f, maximumBendRatio);
            var bendAmount = Mathf.Min(
                Mathf.Min(ratioBend, absoluteBend),
                safeRatioBend);

            // 第一个控制点位于左法线一侧，建立S曲线的第一段弯曲。
            var firstControlPoint = start + chord * safeControlRatio +
                                    chordNormal * bendAmount;

            // 第二个控制点使用完全对称的纵向比例和反向法线偏移。
            var secondControlPoint = start + chord * (1f - safeControlRatio) -
                                     chordNormal * bendAmount;

            return new UIFlowPath2D(
                start,
                firstControlPoint,
                secondControlPoint,
                end,
                chordDirection,
                chordLength);
        }

        /// <summary>
        ///  使用调用方已经确定的四个控制点创建二维三次贝塞尔路径。
        /// </summary>
        /// <remarks>
        ///  该入口用于把Controller坐标空间中的统一路径转换到每个Graphic本地空间，
        ///  不会重新计算法线或弯曲量，因此所有子物体能够保持完全相同的主路径形状。
        /// </remarks>
        /// <param name="start">路径起点。</param>
        /// <param name="firstControlPoint">路径的第一个控制点。</param>
        /// <param name="secondControlPoint">路径的第二个控制点。</param>
        /// <param name="end">路径终点。</param>
        /// <returns>保留全部指定控制点的不可变二维三次贝塞尔路径。</returns>
        public static UIFlowPath2D CreateCubic(
            Vector2 start,
            Vector2 firstControlPoint,
            Vector2 secondControlPoint,
            Vector2 end)
        {
            // 起终点差值用于建立投影和退化路径需要的稳定弦方向。
            var chord = end - start;

            // 缓存弦长，后续顶点形变和Ribbon无需重复计算平方根。
            var chordLength = chord.magnitude;

            // 起终点重合时使用固定右方向，禁止标准化零向量产生无效数值。
            var chordDirection = chordLength > MinimumPathLength
                ? chord / chordLength
                : Vector2.right;

            // 直接保存调用方控制点，统一主路径不会被每个子物体再次派生。
            return new UIFlowPath2D(
                start,
                firstControlPoint,
                secondControlPoint,
                end,
                chordDirection,
                chordLength);
        }

        /// <summary>
        ///  计算指定零到一参数处的完整路径姿态。
        /// </summary>
        /// <param name="pathProgress">尚未附加时间缓动的路径参数。</param>
        /// <returns>包含位置、单位切线、单位法线和绝对曲率的路径姿态。</returns>
        public UIFlowPose Evaluate(float pathProgress)
        {
            // 所有公开采样入口统一限制参数，异常调用不会越过曲线端点。
            var progress = Mathf.Clamp01(pathProgress);
            var position = EvaluatePositionUnclamped(progress);
            var derivative = EvaluateDerivativeUnclamped(progress);
            var derivativeLengthSquared = derivative.sqrMagnitude;

            // 控制点重合或极短路径使用弦方向，保证刚性主体和Ribbon都拥有有效坐标系。
            var tangent = derivativeLengthSquared > MinimumPathLength * MinimumPathLength
                ? derivative / Mathf.Sqrt(derivativeLengthSquared)
                : ChordDirection;
            var normal = new Vector2(-tangent.y, tangent.x);

            // 二维曲率使用叉积绝对值除以一阶导长度的三次方。
            var curvature = 0f;
            if (derivativeLengthSquared > MinimumPathLength * MinimumPathLength)
            {
                var secondDerivative = EvaluateSecondDerivativeUnclamped(progress);
                var cross = derivative.x * secondDerivative.y -
                            derivative.y * secondDerivative.x;
                var derivativeLengthCubed = derivativeLengthSquared *
                                            Mathf.Sqrt(derivativeLengthSquared);
                curvature = Mathf.Abs(cross) / derivativeLengthCubed;
            }

            return new UIFlowPose(position, tangent, normal, curvature);
        }

        /// <summary>
        ///  计算指定零到一参数处的路径位置。
        /// </summary>
        public Vector2 EvaluatePosition(float pathProgress)
        {
            return EvaluatePositionUnclamped(Mathf.Clamp01(pathProgress));
        }

        /// <summary>
        ///  计算指定零到一参数处的一阶导数。
        /// </summary>
        public Vector2 EvaluateDerivative(float pathProgress)
        {
            return EvaluateDerivativeUnclamped(Mathf.Clamp01(pathProgress));
        }

        /// <summary>
        ///  计算指定零到一参数处的二阶导数。
        /// </summary>
        public Vector2 EvaluateSecondDerivative(float pathProgress)
        {
            return EvaluateSecondDerivativeUnclamped(Mathf.Clamp01(pathProgress));
        }

        /// <summary>
        ///  使用固定线段数近似计算整条曲线的弧长。
        /// </summary>
        /// <param name="segmentCount">用于折线近似的线段数量。</param>
        /// <returns>当前本地坐标空间中的近似弧长。</returns>
        public float ApproximateLength(int segmentCount = 48)
        {
            // 至少使用一个线段，防止除零并完整覆盖起点与终点。
            var safeSegmentCount = Mathf.Max(1, segmentCount);
            var previousPosition = Start;
            var totalLength = 0f;
            for (var index = 1; index <= safeSegmentCount; index++)
            {
                var progress = index / (float)safeSegmentCount;
                var currentPosition = EvaluatePositionUnclamped(progress);
                totalLength += Vector2.Distance(previousPosition, currentPosition);
                previousPosition = currentPosition;
            }

            return totalLength;
        }

        /// <summary>
        ///  使用调用方提供的固定数组构建零分配累计弧长表。
        /// </summary>
        /// <param name="cumulativeLengths">长度至少为二的可复用累计弧长数组。</param>
        /// <returns>当前曲线的近似总弧长。</returns>
        /// <exception cref="ArgumentNullException">累计弧长数组为空时抛出。</exception>
        /// <exception cref="ArgumentException">累计弧长数组长度不足时抛出。</exception>
        public float BuildArcLengthTable(float[] cumulativeLengths)
        {
            ValidateArcLengthTable(cumulativeLengths);

            // 数组首项严格对应路径起点，后续每项保存从起点累计到该采样点的长度。
            cumulativeLengths[0] = 0f;
            var segmentCount = cumulativeLengths.Length - 1;
            var previousPosition = Start;
            var totalLength = 0f;
            for (var index = 1; index <= segmentCount; index++)
            {
                var progress = index / (float)segmentCount;
                var currentPosition = EvaluatePositionUnclamped(progress);
                totalLength += Vector2.Distance(previousPosition, currentPosition);
                cumulativeLengths[index] = totalLength;
                previousPosition = currentPosition;
            }

            return totalLength;
        }

        /// <summary>
        ///  使用预先构建的累计表取得指定参数对应的近似弧长。
        /// </summary>
        /// <param name="pathProgress">零到一之间的路径参数。</param>
        /// <param name="cumulativeLengths">由当前路径构建的累计弧长表。</param>
        /// <returns>从路径起点到指定参数处的近似弧长。</returns>
        public float GetLengthAtParameter(
            float pathProgress,
            float[] cumulativeLengths)
        {
            ValidateArcLengthTable(cumulativeLengths);

            // 参数落在两个表项之间时使用线性插值，避免Ribbon站点产生阶梯跳动。
            var segmentCount = cumulativeLengths.Length - 1;
            var tablePosition = Mathf.Clamp01(pathProgress) * segmentCount;
            var lowerIndex = Mathf.Min(Mathf.FloorToInt(tablePosition), segmentCount - 1);
            var upperIndex = lowerIndex + 1;
            var interpolation = tablePosition - lowerIndex;
            return Mathf.LerpUnclamped(
                cumulativeLengths[lowerIndex],
                cumulativeLengths[upperIndex],
                interpolation);
        }

        /// <summary>
        ///  使用预先构建的累计表把指定弧长反查为零到一路径参数。
        /// </summary>
        /// <param name="arcLength">从路径起点开始计算的本地弧长。</param>
        /// <param name="cumulativeLengths">由当前路径构建的累计弧长表。</param>
        /// <returns>与指定弧长对应的近似路径参数。</returns>
        public float GetParameterAtLength(
            float arcLength,
            float[] cumulativeLengths)
        {
            ValidateArcLengthTable(cumulativeLengths);

            // 退化路径的全部采样点重合，稳定返回起点参数。
            var lastIndex = cumulativeLengths.Length - 1;
            var totalLength = cumulativeLengths[lastIndex];
            if (totalLength <= MinimumPathLength) return 0f;

            // 二分查找第一个不小于目标弧长的表项，固定数组不会产生任何枚举器或临时集合。
            var targetLength = Mathf.Clamp(arcLength, 0f, totalLength);
            var lowerIndex = 0;
            var upperIndex = lastIndex;
            while (upperIndex - lowerIndex > 1)
            {
                var middleIndex = (lowerIndex + upperIndex) >> 1;
                if (cumulativeLengths[middleIndex] < targetLength)
                    lowerIndex = middleIndex;
                else
                    upperIndex = middleIndex;
            }

            // 相邻采样点重合时保留较早参数，禁止除零产生无效Ribbon顶点。
            var lowerLength = cumulativeLengths[lowerIndex];
            var upperLength = cumulativeLengths[upperIndex];
            var intervalLength = upperLength - lowerLength;
            var interpolation = intervalLength > MinimumPathLength
                ? (targetLength - lowerLength) / intervalLength
                : 0f;
            return (lowerIndex + interpolation) / lastIndex;
        }

        /// <summary>
        ///  计算已经限制参数后的三次贝塞尔位置。
        /// </summary>
        private Vector2 EvaluatePositionUnclamped(float progress)
        {
            var inverseProgress = 1f - progress;
            var inverseProgressSquared = inverseProgress * inverseProgress;
            var progressSquared = progress * progress;
            return Start * (inverseProgressSquared * inverseProgress) +
                   FirstControlPoint * (3f * inverseProgressSquared * progress) +
                   SecondControlPoint * (3f * inverseProgress * progressSquared) +
                   End * (progressSquared * progress);
        }

        /// <summary>
        ///  计算已经限制参数后的三次贝塞尔一阶导数。
        /// </summary>
        private Vector2 EvaluateDerivativeUnclamped(float progress)
        {
            var inverseProgress = 1f - progress;
            return 3f * inverseProgress * inverseProgress *
                   (FirstControlPoint - Start) +
                   6f * inverseProgress * progress *
                   (SecondControlPoint - FirstControlPoint) +
                   3f * progress * progress *
                   (End - SecondControlPoint);
        }

        /// <summary>
        ///  计算已经限制参数后的三次贝塞尔二阶导数。
        /// </summary>
        private Vector2 EvaluateSecondDerivativeUnclamped(float progress)
        {
            var inverseProgress = 1f - progress;
            return 6f * inverseProgress *
                   (SecondControlPoint - FirstControlPoint * 2f + Start) +
                   6f * progress *
                   (End - SecondControlPoint * 2f + FirstControlPoint);
        }

        /// <summary>
        ///  校验调用方提供的累计弧长数组是否可以覆盖至少一个线段。
        /// </summary>
        private static void ValidateArcLengthTable(float[] cumulativeLengths)
        {
            if (cumulativeLengths == null)
                throw new ArgumentNullException(nameof(cumulativeLengths));
            if (cumulativeLengths.Length < 2)
                throw new ArgumentException(
                    "累计弧长数组至少需要两个元素。",
                    nameof(cumulativeLengths));
        }
    }
}

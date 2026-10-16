using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace JH
{
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
        /// <param name="start">路径起点。</param>
        /// <param name="firstControlPoint">三次贝塞尔路径的第一个控制点。</param>
        /// <param name="secondControlPoint">三次贝塞尔路径的第二个控制点。</param>
        /// <param name="end">路径终点。</param>
        /// <param name="chordDirection">从起点指向终点的单位弦方向。</param>
        /// <param name="chordLength">起终点之间的直线距离。</param>
        private UIFlowPath2D(
            Vector2 start,
            Vector2 firstControlPoint,
            Vector2 secondControlPoint,
            Vector2 end,
            Vector2 chordDirection,
            float chordLength)
        {
            // 保存路径起点。
            Start = start;
            // 保存靠近起点一侧的贝塞尔控制点。
            FirstControlPoint = firstControlPoint;
            // 保存靠近终点一侧的贝塞尔控制点。
            SecondControlPoint = secondControlPoint;
            // 保存路径终点。
            End = end;
            // 保存起终点之间的单位弦方向，供退化采样降级。
            ChordDirection = chordDirection;
            // 保存弦长，避免后续投影和退化检查重复计算。
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
            // 计算弦长，为控制点比例和安全上限提供统一尺度。
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
            // 将绝对弯曲上限限制为非负值。
            var absoluteBend = Mathf.Max(0f, maximumBend);
            // 把相对弯曲上限换算为当前路径的本地距离。
            var safeRatioBend = chordLength * Mathf.Max(0f, maximumBendRatio);
            // 取三种限制中的最小弯曲量，防止路径产生过大横向偏移。
            var bendAmount = Mathf.Min(
                Mathf.Min(ratioBend, absoluteBend),
                safeRatioBend);

            // 第一个控制点位于左法线一侧，建立S曲线的第一段弯曲。
            var firstControlPoint = start + chord * safeControlRatio +
                                    chordNormal * bendAmount;

            // 第二个控制点使用完全对称的纵向比例和反向法线偏移。
            var secondControlPoint = start + chord * (1f - safeControlRatio) -
                                     chordNormal * bendAmount;

            // 把对称控制点与弦信息封装为不可变路径。
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
            // 按限制后的参数采样路径位置。
            var position = EvaluatePositionUnclamped(progress);
            // 计算一阶导数，用于确定前进方向。
            var derivative = EvaluateDerivativeUnclamped(progress);
            // 缓存导数长度平方，避免重复求模并支持退化判断。
            var derivativeLengthSquared = derivative.sqrMagnitude;

            // 控制点重合或极短路径使用弦方向，保证刚性主体和Ribbon都拥有有效坐标系。
            var tangent = derivativeLengthSquared > MinimumPathLength * MinimumPathLength
                ? derivative / Mathf.Sqrt(derivativeLengthSquared)
                : ChordDirection;
            // 将单位切线逆时针旋转九十度得到法线。
            var normal = new Vector2(-tangent.y, tangent.x);

            // 二维曲率使用叉积绝对值除以一阶导长度的三次方。
            var curvature = 0f;
            // 仅有效的一阶导数才参与曲率除法，避免退化路径除零。
            if (derivativeLengthSquared > MinimumPathLength * MinimumPathLength)
            {
                // 采样二阶导数以计算切线变化率。
                var secondDerivative = EvaluateSecondDerivativeUnclamped(progress);
                // 二维叉积给出一阶与二阶导数之间的有向面积。
                var cross = derivative.x * secondDerivative.y -
                            derivative.y * secondDerivative.x;
                // 导数模长的三次方作为曲率公式分母。
                var derivativeLengthCubed = derivativeLengthSquared *
                                            Mathf.Sqrt(derivativeLengthSquared);
                // 使用叉积绝对值获得与路径方向无关的非负曲率。
                curvature = Mathf.Abs(cross) / derivativeLengthCubed;
            }

            // 返回同一次采样得到的位置、方向、法线与曲率。
            return new UIFlowPose(position, tangent, normal, curvature);
        }

        /// <summary>
        ///  计算指定零到一参数处的路径位置。
        /// </summary>
        /// <param name="pathProgress">将在采样前限制为零到一的路径参数。</param>
        /// <returns>指定参数处的路径位置。</returns>
        public Vector2 EvaluatePosition(float pathProgress)
        {
            // 限制公开参数后复用不重复钳制的贝塞尔位置计算。
            return EvaluatePositionUnclamped(Mathf.Clamp01(pathProgress));
        }

        /// <summary>
        ///  计算指定零到一参数处的一阶导数。
        /// </summary>
        /// <param name="pathProgress">将在采样前限制为零到一的路径参数。</param>
        /// <returns>指定参数处的一阶导数向量。</returns>
        public Vector2 EvaluateDerivative(float pathProgress)
        {
            // 限制公开参数后计算一阶导数。
            return EvaluateDerivativeUnclamped(Mathf.Clamp01(pathProgress));
        }

        /// <summary>
        ///  计算指定零到一参数处的二阶导数。
        /// </summary>
        /// <param name="pathProgress">将在采样前限制为零到一的路径参数。</param>
        /// <returns>指定参数处的二阶导数向量。</returns>
        public Vector2 EvaluateSecondDerivative(float pathProgress)
        {
            // 限制公开参数后计算二阶导数。
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
            // 折线近似从路径起点开始累计。
            var previousPosition = Start;
            // 将累计长度初始化为零。
            var totalLength = 0f;
            // 依次采样全部线段终点，包含完整路径终点。
            for (var index = 1; index <= safeSegmentCount; index++)
            {
                // 把线段索引映射为零到一路径参数。
                var progress = index / (float)safeSegmentCount;
                // 取得当前折线段的末端采样位置。
                var currentPosition = EvaluatePositionUnclamped(progress);
                // 累加相邻两个采样点之间的直线距离。
                totalLength += Vector2.Distance(previousPosition, currentPosition);
                // 把当前采样点作为下一线段的起点。
                previousPosition = currentPosition;
            }

            // 返回全部近似线段的长度之和。
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
            // 先验证缓冲区能够同时保存起点与终点。
            ValidateArcLengthTable(cumulativeLengths);

            // 数组首项严格对应路径起点，后续每项保存从起点累计到该采样点的长度。
            cumulativeLengths[0] = 0f;
            // 相邻数组项之间形成一段折线，段数比数组长度少一。
            var segmentCount = cumulativeLengths.Length - 1;
            // 从路径起点建立首段折线。
            var previousPosition = Start;
            // 累计弧长从零开始。
            var totalLength = 0f;
            // 遍历每个后续表项并包含路径终点。
            for (var index = 1; index <= segmentCount; index++)
            {
                // 按固定表项数均匀分配路径参数。
                var progress = index / (float)segmentCount;
                // 计算当前表项对应的贝塞尔位置。
                var currentPosition = EvaluatePositionUnclamped(progress);
                // 增加这一小段折线的长度。
                totalLength += Vector2.Distance(previousPosition, currentPosition);
                // 保存从起点累计至当前采样点的长度。
                cumulativeLengths[index] = totalLength;
                // 更新下一段折线的起点。
                previousPosition = currentPosition;
            }

            // 返回末项累计长度，供调用方缓存总弧长。
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
            // 验证表至少包含一个可插值区间。
            ValidateArcLengthTable(cumulativeLengths);

            // 参数落在两个表项之间时使用线性插值，避免Ribbon站点产生阶梯跳动。
            var segmentCount = cumulativeLengths.Length - 1;
            // 把限制后的路径参数换算为连续表项位置。
            var tablePosition = Mathf.Clamp01(pathProgress) * segmentCount;
            // 选取左侧表项，并让终点仍落在最后一个有效区间。
            var lowerIndex = Mathf.Min(Mathf.FloorToInt(tablePosition), segmentCount - 1);
            // 右侧表项始终与左侧相邻。
            var upperIndex = lowerIndex + 1;
            // 计算目标位置在两个表项之间的局部权重。
            var interpolation = tablePosition - lowerIndex;
            // 对相邻累计长度插值，得到目标参数的近似弧长。
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
            // 验证累计表后才读取末项和二分边界。
            ValidateArcLengthTable(cumulativeLengths);

            // 退化路径的全部采样点重合，稳定返回起点参数。
            var lastIndex = cumulativeLengths.Length - 1;
            // 末项保存整条路径的近似总长度。
            var totalLength = cumulativeLengths[lastIndex];
            // 所有采样点重合时稳定返回起点参数。
            if (totalLength <= MinimumPathLength) return 0f;

            // 二分查找第一个不小于目标弧长的表项，固定数组不会产生任何枚举器或临时集合。
            var targetLength = Mathf.Clamp(arcLength, 0f, totalLength);
            // 二分区间从路径起点表项开始。
            var lowerIndex = 0;
            // 二分区间右端初始为路径终点表项。
            var upperIndex = lastIndex;
            // 将搜索区间缩小到两个相邻表项。
            while (upperIndex - lowerIndex > 1)
            {
                // 取当前上下界的整数中点。
                var middleIndex = (lowerIndex + upperIndex) >> 1;
                // 中点长度未达到目标时继续搜索右半段。
                if (cumulativeLengths[middleIndex] < targetLength)
                    // 将较小弧长的下界推进到中点。
                    lowerIndex = middleIndex;
                else
                    // 中点已达到目标时将上界收缩到中点。
                    upperIndex = middleIndex;
            }

            // 相邻采样点重合时保留较早参数，禁止除零产生无效Ribbon顶点。
            var lowerLength = cumulativeLengths[lowerIndex];
            // 取得右侧表项的累计长度。
            var upperLength = cumulativeLengths[upperIndex];
            // 计算相邻采样点之间的弧长区间。
            var intervalLength = upperLength - lowerLength;
            // 有效区间按距离求权重，重合采样点保留较早参数。
            var interpolation = intervalLength > MinimumPathLength
                ? (targetLength - lowerLength) / intervalLength
                : 0f;
            // 把表项位置和局部权重还原为归一化路径参数。
            return (lowerIndex + interpolation) / lastIndex;
        }

        /// <summary>
        ///  计算已经限制参数后的三次贝塞尔位置。
        /// </summary>
        /// <param name="progress">调用方已限制到零到一的路径参数。</param>
        /// <returns>未再次限制参数的三次贝塞尔位置。</returns>
        private Vector2 EvaluatePositionUnclamped(float progress)
        {
            // 缓存参数的补量，用于起点一侧的贝塞尔基函数。
            var inverseProgress = 1f - progress;
            // 缓存补量平方以复用相邻项的乘法结果。
            var inverseProgressSquared = inverseProgress * inverseProgress;
            // 缓存参数平方以复用终点一侧的基函数。
            var progressSquared = progress * progress;
            // 按三次 Bernstein 基函数加权四个控制点。
            return Start * (inverseProgressSquared * inverseProgress) +
                   FirstControlPoint * (3f * inverseProgressSquared * progress) +
                   SecondControlPoint * (3f * inverseProgress * progressSquared) +
                   End * (progressSquared * progress);
        }

        /// <summary>
        ///  计算已经限制参数后的三次贝塞尔一阶导数。
        /// </summary>
        /// <param name="progress">调用方已限制到零到一的路径参数。</param>
        /// <returns>未再次限制参数的一阶导数向量。</returns>
        private Vector2 EvaluateDerivativeUnclamped(float progress)
        {
            // 缓存参数补量，供一阶导数的二次基函数使用。
            var inverseProgress = 1f - progress;
            // 对相邻控制点差值按三次贝塞尔一阶导公式加权。
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
        /// <param name="progress">调用方已限制到零到一的路径参数。</param>
        /// <returns>未再次限制参数的二阶导数向量。</returns>
        private Vector2 EvaluateSecondDerivativeUnclamped(float progress)
        {
            // 缓存参数补量，用于二阶导数的线性组合。
            var inverseProgress = 1f - progress;
            // 对两组三点二阶差分加权得到二阶导数。
            return 6f * inverseProgress *
                   (SecondControlPoint - FirstControlPoint * 2f + Start) +
                   6f * progress *
                   (End - SecondControlPoint * 2f + FirstControlPoint);
        }

        /// <summary>
        ///  校验调用方提供的累计弧长数组是否可以覆盖至少一个线段。
        /// </summary>
        /// <param name="cumulativeLengths">至少包含两个元素的累计弧长数组。</param>
        private static void ValidateArcLengthTable(float[] cumulativeLengths)
        {
            // 空数组无法承载起终点弧长信息。
            if (cumulativeLengths == null)
                // 用参数名报告缺失的累计弧长缓冲区。
                throw new ArgumentNullException(nameof(cumulativeLengths));
            // 少于两个元素无法构成任何有效弧长区间。
            if (cumulativeLengths.Length < 2)
                // 报告数组容量不足，并保留明确的最低容量说明。
                throw new ArgumentException(
                    "累计弧长数组至少需要两个元素。",
                    nameof(cumulativeLengths));
        }
    }
}

using System;
using UnityEngine;

namespace JH
{
    /// <summary>UI 过渡内部使用的缓动曲线计算。</summary>
    internal static class UIEasing
    {
        /// <summary>将归一化进度映射到缓动曲线；允许曲线自身产生过冲。</summary>
        /// <param name="ease">要采样的缓动类型；未设置或未知值按线性处理。</param>
        /// <param name="f">采样前会限制到零到一的时间进度。</param>
        /// <returns>对应曲线的插值权重，弹性或回退曲线可能超出零到一。</returns>
        internal static float Evaluate(Ease ease, float f)
        {
            // 先限制输入时间，保留曲线输出自身的回退、过冲和弹跳。
            f = Mathf.Clamp01(f);
            // 用双精度计算曲线，再转换为 Unity 插值接口使用的单精度权重。
            double t = ease switch
            {
                Ease.Linear => f,

                // 正弦曲线以平滑的三角函数速度进入或离开端点。
                Ease.InSine => 1 - Math.Cos(f * Math.PI / 2),
                Ease.OutSine => Math.Sin(f * Math.PI / 2),
                Ease.InOutSine => -(Math.Cos(Math.PI * f) - 1) / 2,

                // 二次多项式分别描述加速、减速与前后对称的速度变化。
                Ease.InQuad => f * f,
                Ease.OutQuad => 1 - (1 - f) * (1 - f),
                Ease.InOutQuad => f < 0.5 ? 2 * f * f : 1 - Math.Pow(-2 * f + 2, 2) / 2,

                // 三次多项式使端点附近的速度变化比二次曲线更集中。
                Ease.InCubic => f * f * f,
                Ease.OutCubic => 1 - Math.Pow(1 - f, 3),
                Ease.InOutCubic => f < 0.5 ? 4 * f * f * f : 1 - Math.Pow(-2 * f + 2, 3) / 2,

                // 四次多项式进一步压低缓入端或缓出端的速度。
                Ease.InQuart => f * f * f * f,
                Ease.OutQuart => 1 - Math.Pow(1 - f, 4),
                Ease.InOutQuart => f < 0.5 ? 8 * f * f * f * f : 1 - Math.Pow(-2 * f + 2, 4) / 2,

                // 五次多项式提供更显著的前慢后快或前快后慢变化。
                Ease.InQuint => f * f * f * f * f,
                Ease.OutQuint => 1 - Math.Pow(1 - f, 5),
                Ease.InOutQuint => f < 0.5 ? 16 * f * f * f * f * f : 1 - Math.Pow(-2 * f + 2, 5) / 2,

                // 指数曲线单独处理精确端点，避免指数尾部无法达到零或一。
                Ease.InExpo => Mathf.Approximately(f, 0) ? 0 : Math.Pow(2, 10 * f - 10),
                Ease.OutExpo => Mathf.Approximately(f, 1) ? 1 : 1 - Math.Pow(2, -10 * f),
                Ease.InOutExpo => Mathf.Approximately(f, 0) ? 0 :
                    Mathf.Approximately(f, 1) ? 1 :
                    f < 0.5 ? Math.Pow(2, 20 * f - 10) / 2 : (2 - Math.Pow(2, -20 * f + 10)) / 2,

                // 圆弧曲线通过单位圆方程生成速度变化。
                Ease.InCirc => 1 - Math.Sqrt(1 - Math.Pow(f, 2)),
                Ease.OutCirc => Math.Sqrt(1 - Math.Pow(f - 1, 2)),
                Ease.InOutCirc => f < 0.5
                    ? (1 - Math.Sqrt(1 - Math.Pow(2 * f, 2))) / 2
                    : (Math.Sqrt(1 - Math.Pow(-2 * f + 2, 2)) + 1) / 2,

                // 回退曲线先越过起点或终点，再回到目标权重。
                Ease.InBack => 2.70158 * f * f * f - 1.70158 * f * f,
                Ease.OutBack => 1 + 2.70158 * Math.Pow(f - 1, 3) + 1.70158 * Math.Pow(f - 1, 2),
                Ease.InOutBack => f < 0.5
                    ? Math.Pow(2 * f, 2) * ((2.5949095 + 1) * 2 * f - 2.5949095) / 2
                    : (Math.Pow(2 * f - 2, 2) * ((2.5949095 + 1) * (f * 2 - 2) + 2.5949095) + 2) / 2,

                // 弹性缓入由增长的指数包络调制正弦振荡，精确保留端点。
                Ease.InElastic => Mathf.Approximately(f, 0) ? 0 :
                    Mathf.Approximately(f, 1) ? 1 :
                    -Math.Pow(2, 10 * f - 10) * Math.Sin((f * 10 - 10.75) * (2 * Math.PI / 3)),

                // 弹性缓出用衰减振荡收敛到终点。
                Ease.OutElastic => Mathf.Approximately(f, 0) ? 0 :
                    Mathf.Approximately(f, 1) ? 1 :
                    Math.Pow(2, -10 * f) * Math.Sin((f * 10 - 0.75) * (2 * Math.PI / 3)) + 1,

                // 弹性缓入缓出在中点两侧分别使用增长和衰减包络。
                Ease.InOutElastic => Mathf.Approximately(f, 0) ? 0 :
                    Mathf.Approximately(f, 1) ? 1 :
                    f < 0.5 ? -(Math.Pow(2, 20 * f - 10) * Math.Sin((20 * f - 11.125) * (2 * Math.PI / 4.5))) / 2 :
                    Math.Pow(2, -20 * f + 10) * Math.Sin((20 * f - 11.125) * (2 * Math.PI / 4.5)) / 2 + 1,

                // 弹跳以缓出分段抛物线为基准，通过镜像组合得到其他方向。
                Ease.InBounce => 1 - GetOutBounce(1 - f),
                Ease.OutBounce => GetOutBounce(f),
                Ease.InOutBounce => f < 0.5
                    ? (1 - GetOutBounce(1 - 2 * f)) / 2
                    : (1 + GetOutBounce(2 * f - 1)) / 2,

                _ => f // 默认返回线性插值
            };

            // 数学计算完成后转换为动效系统使用的浮点权重。
            return (float)t;
        }

        /// <summary>按四段抛物线计算由大到小的缓出弹跳。</summary>
        /// <param name="x">零到一的弹跳时间进度。</param>
        /// <returns>当前抛物线区段上的弹跳权重。</returns>
        private static double GetOutBounce(double x)
        {
            // 抛物线系数与分段宽度共同保证各段在接触点连续。
            const double n1 = 7.5625;
            // 统一分母用于划分四次接触的时间区间。
            const double d1 = 2.75;

            // 第一段抛物线区间
            if (x < 1 / d1) return n1 * x * x;

            // 第二段抛物线区间
            if (x < 2 / d1)
            {
                // 平移到第二段抛物线的局部时间原点。
                x -= 1.5 / d1;
                // 叠加第二次弹跳高度，使这一段衔接前后接触点。
                return n1 * x * x + 0.75;
            }

            // 第三段抛物线区间
            if (x < 2.5 / d1)
            {
                // 第三次弹跳缩短时间范围并进一步降低幅度。
                x -= 2.25 / d1;
                // 用较高基线形成接近终点的小幅回弹。
                return n1 * x * x + 0.9375;
            }

            // 末段抛物线区间
            x -= 2.625 / d1;
            // 最后一次微小回弹精确收敛到终值一。
            return n1 * x * x + 0.984375;
        }
    }
}

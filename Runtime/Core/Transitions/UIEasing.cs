using System;
using UnityEngine;

namespace JH
{
    /// <summary>UI 过渡内部使用的缓动曲线计算。</summary>
    internal static class UIEasing
    {
        /// <summary>将归一化进度映射到缓动曲线；允许曲线自身产生过冲。</summary>
        internal static float Evaluate(Ease ease, float f)
        {
            f = Mathf.Clamp01(f);
            double t = ease switch
            {
                Ease.Linear => f,

                // Sine
                Ease.InSine => 1 - Math.Cos(f * Math.PI / 2),
                Ease.OutSine => Math.Sin(f * Math.PI / 2),
                Ease.InOutSine => -(Math.Cos(Math.PI * f) - 1) / 2,

                // Quad
                Ease.InQuad => f * f,
                Ease.OutQuad => 1 - (1 - f) * (1 - f),
                Ease.InOutQuad => f < 0.5 ? 2 * f * f : 1 - Math.Pow(-2 * f + 2, 2) / 2,

                // Cubic
                Ease.InCubic => f * f * f,
                Ease.OutCubic => 1 - Math.Pow(1 - f, 3),
                Ease.InOutCubic => f < 0.5 ? 4 * f * f * f : 1 - Math.Pow(-2 * f + 2, 3) / 2,

                // Quart
                Ease.InQuart => f * f * f * f,
                Ease.OutQuart => 1 - Math.Pow(1 - f, 4),
                Ease.InOutQuart => f < 0.5 ? 8 * f * f * f * f : 1 - Math.Pow(-2 * f + 2, 4) / 2,

                // Quint
                Ease.InQuint => f * f * f * f * f,
                Ease.OutQuint => 1 - Math.Pow(1 - f, 5),
                Ease.InOutQuint => f < 0.5 ? 16 * f * f * f * f * f : 1 - Math.Pow(-2 * f + 2, 5) / 2,

                // Expo
                Ease.InExpo => Mathf.Approximately(f, 0) ? 0 : Math.Pow(2, 10 * f - 10),
                Ease.OutExpo => Mathf.Approximately(f, 1) ? 1 : 1 - Math.Pow(2, -10 * f),
                Ease.InOutExpo => Mathf.Approximately(f, 0) ? 0 :
                    Mathf.Approximately(f, 1) ? 1 :
                    f < 0.5 ? Math.Pow(2, 20 * f - 10) / 2 : (2 - Math.Pow(2, -20 * f + 10)) / 2,

                // Circ
                Ease.InCirc => 1 - Math.Sqrt(1 - Math.Pow(f, 2)),
                Ease.OutCirc => Math.Sqrt(1 - Math.Pow(f - 1, 2)),
                Ease.InOutCirc => f < 0.5
                    ? (1 - Math.Sqrt(1 - Math.Pow(2 * f, 2))) / 2
                    : (Math.Sqrt(1 - Math.Pow(-2 * f + 2, 2)) + 1) / 2,

                // Back
                Ease.InBack => 2.70158 * f * f * f - 1.70158 * f * f,
                Ease.OutBack => 1 + 2.70158 * Math.Pow(f - 1, 3) + 1.70158 * Math.Pow(f - 1, 2),
                Ease.InOutBack => f < 0.5
                    ? Math.Pow(2 * f, 2) * ((2.5949095 + 1) * 2 * f - 2.5949095) / 2
                    : (Math.Pow(2 * f - 2, 2) * ((2.5949095 + 1) * (f * 2 - 2) + 2.5949095) + 2) / 2,

                // Elastic
                Ease.InElastic => Mathf.Approximately(f, 0) ? 0 :
                    Mathf.Approximately(f, 1) ? 1 :
                    -Math.Pow(2, 10 * f - 10) * Math.Sin((f * 10 - 10.75) * (2 * Math.PI / 3)),

                // OutElastic
                Ease.OutElastic => Mathf.Approximately(f, 0) ? 0 :
                    Mathf.Approximately(f, 1) ? 1 :
                    Math.Pow(2, -10 * f) * Math.Sin((f * 10 - 0.75) * (2 * Math.PI / 3)) + 1,

                // InOutElastic
                Ease.InOutElastic => Mathf.Approximately(f, 0) ? 0 :
                    Mathf.Approximately(f, 1) ? 1 :
                    f < 0.5 ? -(Math.Pow(2, 20 * f - 10) * Math.Sin((20 * f - 11.125) * (2 * Math.PI / 4.5))) / 2 :
                    Math.Pow(2, -20 * f + 10) * Math.Sin((20 * f - 11.125) * (2 * Math.PI / 4.5)) / 2 + 1,

                // Bounce
                Ease.InBounce => 1 - GetOutBounce(1 - f),
                Ease.OutBounce => GetOutBounce(f),
                Ease.InOutBounce => f < 0.5
                    ? (1 - GetOutBounce(1 - 2 * f)) / 2
                    : (1 + GetOutBounce(2 * f - 1)) / 2,

                _ => f // 默认返回线性插值
            };

            return (float)t;
        }

        private static double GetOutBounce(double x)
        {
            const double n1 = 7.5625;
            const double d1 = 2.75;

            // 第一段抛物线区间
            if (x < 1 / d1) return n1 * x * x;

            // 第二段抛物线区间
            if (x < 2 / d1)
            {
                x -= 1.5 / d1;
                return n1 * x * x + 0.75;
            }

            // 第三段抛物线区间
            if (x < 2.5 / d1)
            {
                x -= 2.25 / d1;
                return n1 * x * x + 0.9375;
            }

            // 末段抛物线区间
            x -= 2.625 / d1;
            return n1 * x * x + 0.984375;
        }
    }
}

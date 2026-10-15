using System;

namespace JH
{
    /// <summary>UI 显隐调度的公共配置。</summary>
    public static class UIOperationSettings
    {
        private static int _maxExecutionsPerBehavior = 10;

        /// <summary>
        /// 同一条来源链中，同一个 UI 实例的同一种显隐行为最多实际执行的次数。
        /// 默认 10，必须大于 0；配置在根请求创建时固定，修改只影响新的来源链。
        /// </summary>
        public static int MaxExecutionsPerBehavior
        {
            get => _maxExecutionsPerBehavior;
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "执行次数上限必须大于0。");
                _maxExecutionsPerBehavior = value;
            }
        }
    }
}

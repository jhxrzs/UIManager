using UnityEngine;

namespace JH
{
    /// <summary>
    ///  通过播放动画来控制显示和隐藏的行为
    /// </summary>
    [RequireComponent(typeof(Animation))]
    public class AnimController : EffectControllerBase
    {
        private Animation _animation;

        protected override void Awake()
        {
            base.Awake();
            _animation = GetComponent<Animation>();
            _animation.playAutomatically = false;
            _animation.Stop();
        }

        /// <summary>
        ///  0 = 完全播放
        ///  1 = 进度为0
        /// </summary>
        protected override void OnEffectAmountChanged(float value)
        {
            if (!_animation) _animation = GetComponent<Animation>();
            // 动态添加后允许先完成组件初始化，再由调用方配置默认动画片段。
            if (!_animation || !_animation.clip) return;
            var duration = _animation.clip.length;
            var setTime = duration * (1 - value);
            _animation.clip.SampleAnimation(gameObject, setTime);
        }
    }
}

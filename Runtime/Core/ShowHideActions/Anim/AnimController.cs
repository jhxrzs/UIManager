using UnityEngine;

namespace JH
{
    /// <summary>
    ///  通过播放动画来控制显示和隐藏的行为
    /// </summary>
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
            _animation ??= GetComponent<Animation>();
            var duration = _animation.clip.length;
            var setTime = duration * (1 - value);
            _animation.clip.SampleAnimation(gameObject, setTime);
        }
    }
}
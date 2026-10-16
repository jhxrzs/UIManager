using UnityEngine;

namespace JH
{
    /// <summary>
    ///  通过播放动画来控制显示和隐藏的行为
    /// </summary>
    [RequireComponent(typeof(Animation))]
    public class AnimController : EffectControllerBase
    {
        /// <summary>缓存用于按时间采样默认片段的旧版动画组件。</summary>
        private Animation _animation;

        /// <summary>订阅显隐生命周期并关闭动画组件的自动播放。</summary>
        protected override void Awake()
        {
            // 先建立显隐订阅，再取得由 RequireComponent 保证存在的动画组件。
            base.Awake();
            // 缓存当前节点上负责片段采样的 Animation。
            _animation = GetComponent<Animation>();
            // 动画时间由动效进度统一控制，不能让自动播放同时写入姿态。
            _animation.playAutomatically = false;
            // 停止已在播放的片段，避免自动时间与手动采样竞争。
            _animation.Stop();
        }

        /// <summary>
        ///  0 = 完全播放
        ///  1 = 进度为0
        /// </summary>
        /// <param name="value">零采样片段末尾，一采样片段起点的隐藏进度。</param>
        protected override void OnEffectAmountChanged(float value)
        {
            // 兼容进度通知早于组件引用缓存完成的动态添加顺序。
            if (!_animation) _animation = GetComponent<Animation>();
            // 动态添加后允许先完成组件初始化，再由调用方配置默认动画片段。
            if (!_animation || !_animation.clip) return;
            // 将隐藏进度反向换算成默认片段的秒数，并直接采样当前对象姿态。
            var duration = _animation.clip.length;
            // 把反向进度换算为默认片段中的采样秒数。
            var setTime = duration * (1 - value);
            // 直接应用该时刻的动画姿态，不启动独立播放计时。
            _animation.clip.SampleAnimation(gameObject, setTime);
        }
    }
}

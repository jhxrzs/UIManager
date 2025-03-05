using System.Threading;

using UnityEngine;

namespace JH
{
    /// <summary>
    ///  利用RectTransform的DoTween动画来显示UI的异步效果
    /// </summary>
    [RequireComponent(typeof(UIBase))]
    public class RectTransformTweenShow : MonoBehaviour
    {
        /// <summary>  
        ///  需要显示的UI面板
        /// </summary>
        [SerializeField, HideInInspector] protected UIBase uiBase;

        /// <summary>
        ///  显示的时间
        /// </summary>
        [SerializeField] protected float duration = 0.3f;

        /// <summary>
        ///  位置动画曲线
        /// </summary>
        [SerializeField] protected Ease anchorPosEase = Ease.Linear;

        /// <summary>
        ///  旋转动画曲线
        /// </summary>
        [SerializeField] protected Ease angleEase = Ease.Linear;

        /// <summary>
        ///  缩放动画曲线
        /// </summary>
        [SerializeField] protected Ease scaleEase = Ease.Linear;

        /// <summary>
        ///  大小动画曲线
        /// </summary>
        [SerializeField] protected Ease sizeDeltaEase = Ease.Linear;

        /// <summary>
        ///   轴心动画曲线
        /// </summary>
        [SerializeField] protected Ease pivotEase = Ease.Linear;

        /// <summary>
        ///  目标位置
        /// </summary>
        [SerializeField] private RectTransform target;

        /// <summary>
        ///   RectTransform组件
        /// </summary>
        [SerializeField, HideInInspector] protected RectTransform rectTransform;

        /// <summary>
        ///  当前的异步任务是否完成
        /// </summary>
        private bool _finish;

        private void OnValidate()
        {
            uiBase = GetComponent<UIBase>();
            rectTransform = GetComponent<RectTransform>();

            // 尝试获取目标位置
            FuzzyFindTransformTool.TryGetTarget(this);
        }

        /// <summary>
        ///  激活脚本时，监听UI面板的异步显示事件
        /// </summary>
        protected virtual void Awake()
        {
            uiBase.ShowAsyncActions += TweenRectTransformAsync;
        }

        /// <summary>
        ///  禁用脚本时，取消监听UI面板的异步显示事件
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (uiBase != null) uiBase.ShowAsyncActions -= TweenRectTransformAsync;
        }

        /// <summary>
        ///   FadeIn效果
        /// </summary>
        protected async JTask TweenRectTransformAsync(CancellationToken token)
        {
            //位置动画
            var anchorPos = rectTransform.Tween(
                rectTransform.anchoredPosition, target.anchoredPosition,
                value => rectTransform.anchoredPosition = value, duration, token, anchorPosEase);

            // 旋转动画
            var angle = rectTransform.Tween(
                rectTransform.localEulerAngles, target.localEulerAngles,
                value => rectTransform.localEulerAngles = value, duration, token, angleEase);

            // 缩放动画
            var scale = rectTransform.Tween(
                rectTransform.localScale, target.localScale,
                value => rectTransform.localScale = value, duration, token, scaleEase);

            // 大小动画
            var sizeDelta = rectTransform.Tween(
                rectTransform.sizeDelta, target.sizeDelta,
                value => rectTransform.sizeDelta = value, duration, token, sizeDeltaEase);

            // 轴心动画
            var pivot = rectTransform.Tween(
                rectTransform.pivot, target.pivot,
                value => rectTransform.pivot = value, duration, token, pivotEase);

            // 等待所有动画完成
            await JTask.WhenAll(anchorPos, angle, scale, sizeDelta, pivot);
        }
    }
}
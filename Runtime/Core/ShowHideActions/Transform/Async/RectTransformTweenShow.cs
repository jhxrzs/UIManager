using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace UIManager
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
        [SerializeField,HideInInspector] protected UIBase uiBase;

        /// <summary>
        ///  显示的时间
        /// </summary>
        [SerializeField] protected float duration = 1f;

        /// <summary>
        ///  动画曲线
        /// </summary>
        [SerializeField] protected Ease tweenEase = Ease.InQuad;

        /// <summary>
        ///  目标位置
        /// </summary>
        [SerializeField] private RectTransform target;

        /// <summary>
        ///   RectTransform组件
        /// </summary>
        [SerializeField,HideInInspector] protected RectTransform rectTransform;

        /// <summary>
        ///  当前的异步任务是否完成
        /// </summary>
        private bool _finish;

        private void OnValidate()
        {
            uiBase = GetComponent<UIBase>();
            rectTransform = GetComponent<RectTransform>();
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
        protected async UniTask TweenRectTransformAsync(CancellationToken token)
        {
            //使用这个flag来阻断异步操作
            _finish = false;

            // 使用DoTween插件实现变换
            rectTransform.DOAnchorPos(target.anchoredPosition, duration).SetEase(tweenEase).SetId(transform);
            rectTransform.DOScale(target.localScale, duration).SetEase(tweenEase).SetId(transform);
            rectTransform.DOLocalRotate(target.localEulerAngles, duration).SetEase(tweenEase).SetId(transform);
            rectTransform.DOSizeDelta(target.sizeDelta, duration).SetEase(tweenEase).SetId(transform);
            rectTransform.DOPivot(target.pivot, duration).SetEase(tweenEase).SetId(transform)
                .OnComplete(() => { _finish = true; });

            // 注册取消异步操作
            token.Register(() => { DOTween.Kill(transform); });
            await UniTask.WaitUntil(() => _finish, cancellationToken: token);

            // 完成后，设置为目标位置
            rectTransform.anchoredPosition = target.anchoredPosition;
            rectTransform.localScale = target.localScale;
            rectTransform.localEulerAngles = target.localEulerAngles;
            rectTransform.sizeDelta = target.sizeDelta;
            rectTransform.pivot = target.pivot;
        }
    }
}
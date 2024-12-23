using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace UIManager
{
    /// <summary>
    /// 逐渐显示的UI动画
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(UIBase))]
    public class FadeIn : MonoBehaviour
    {
        /// <summary>  
        ///  需要逐渐显示的UI面板
        /// </summary>
        [SerializeField,HideInInspector] protected UIBase uiBase;

        /// <summary>
        ///  逐渐显示的时间
        /// </summary>
        [SerializeField] protected float duration = 1f;

        /// <summary>
        ///  动画曲线
        /// </summary>
        [SerializeField] protected Ease tweenEase = Ease.InQuad;

        /// <summary>
        ///  CanvasGroup组件,用来控制UI面板的透明度
        /// </summary>
        [SerializeField, HideInInspector]
        protected CanvasGroup canvasGroup;

        /// <summary>
        ///   目标透明度
        /// </summary>
        protected virtual float EndAlpha => 1;
        
        /// <summary>
        ///  当前的异步任务是否完成
        /// </summary>
        private bool _finish;

        private void OnValidate()
        {
            uiBase = GetComponent<UIBase>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        /// <summary>
        ///  激活脚本时，监听UI面板的异步显示事件，加入FadeIn效果的动画
        /// </summary>
        protected virtual void Awake()
        {
            uiBase.ShowAsyncActions += TweenAlphaAsync;
        }

        /// <summary>
        ///  禁用脚本时，取消监听UI面板的异步显示事件
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (uiBase != null) uiBase.ShowAsyncActions -= TweenAlphaAsync;
        }

        /// <summary>
        ///   FadeIn效果
        /// </summary>
        protected async UniTask TweenAlphaAsync(CancellationToken token)
        {
            //使用这个flag来阻断异步操作
            _finish = false;

            // 使用DoTween插件实现逐渐显示的效果,注册完成flag
            var tween = canvasGroup.DOFade(EndAlpha, duration)
                .SetEase(tweenEase).OnComplete(() => { _finish = true; });
            token.Register(() =>
            {
                tween.Kill();
            });
            await UniTask.WaitUntil(() => _finish, cancellationToken: token);
            canvasGroup.alpha = EndAlpha;
        }
    }
}
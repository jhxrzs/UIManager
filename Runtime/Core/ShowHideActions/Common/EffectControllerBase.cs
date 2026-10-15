using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace JH
{
    /// <summary>
    ///  动效控制基类
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(UIBase))]
    public class EffectControllerBase : MonoBehaviour
    {
        /// <summary>  
        ///  动效应用的的UI面板
        /// </summary>
        [SerializeField, HideInInspector] protected UIBase uiBase;

        /// <summary>
        ///  显示的动效时间
        /// </summary>
        [SerializeField, Tooltip("UI从隐藏到显示的用时")]
        private float showDuration = 1f;

        /// <summary>
        ///  隐藏的动效时间
        /// </summary>
        [SerializeField, Tooltip("UI从显示到隐藏的用时")]
        private float hideDuration = 1f;

        /// <summary>
        ///  UI从隐藏到显示的曲线
        /// </summary>
        [SerializeField, Tooltip("UI从隐藏到显示的曲线")]
        protected Ease showEase = Ease.NotSet;

        /// <summary>
        ///  UI从显示到隐藏的曲线
        /// </summary>
        [SerializeField, Tooltip("UI从显示到隐藏的曲线")]
        protected Ease hideEase = Ease.NotSet;

        /// <summary>
        ///  动效进度：0 == 显示，1 == 隐藏
        /// </summary>
        [SerializeField, Range(0, 1), Tooltip("动效进度：0 == 显示，1 == 隐藏")]
        protected float effectAmount;

        /// <summary>
        ///  隐藏时不允许交互
        /// </summary>
        [SerializeField, Tooltip("隐藏时不再阻挡射线,无法交互")]
        protected bool dontBlockRaycastOnHide = true;

        /// <summary>
        ///  CanvasGroup组件,用来控制UI面板的透明度
        /// </summary>
        [SerializeField, HideInInspector] protected CanvasGroup canvasGroup;

        /// <summary>
        ///  获取CanvasGroup组件
        /// </summary>
        protected CanvasGroup CanvasGroup =>
            canvasGroup ?? GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        /// <summary>
        ///  动效进度
        /// </summary>
        protected float EffectAmount
        {
            get => effectAmount;
            set
            {
                effectAmount = Mathf.Clamp01(value);
                OnEffectAmountChanged(effectAmount);

                // 只要不是完全显示，就不允许点击
                if (dontBlockRaycastOnHide) CanvasGroup.blocksRaycasts = effectAmount == 0f;
            }
        }

        /// <summary>
        ///  激活脚本时，监听UI面板的异步显示事件，加入FadeIn效果的动画
        /// </summary>
        protected virtual void Awake()
        {
            uiBase.ShowAsyncActions += OnUIShowAsync;
            uiBase.HideAsyncActions += OnUIHideAsync;
            uiBase.ShowActions += OnUIShow;
            uiBase.HideActions += OnUIHide;
        }

        /// <summary>
        ///  当UI面板隐藏时
        /// </summary>
        protected virtual void OnUIHide()
        {
            EffectAmount = 1;
        }

        /// <summary>
        ///  当UI面板显示时
        /// </summary>
        protected virtual void OnUIShow()
        {
            EffectAmount = 0;
        }

        /// <summary>
        ///  UI面板隐藏时的动效
        /// </summary>
        protected virtual async UniTask OnUIHideAsync(CancellationToken token)
        {
            await UITransition.RunAsync(EffectAmount, 1, value => EffectAmount = value, hideDuration, token,
                hideEase == Ease.NotSet ? Ease.Linear : hideEase);
        }

        /// <summary>
        ///  UI面板显示时的动效
        /// </summary>
        protected virtual async UniTask OnUIShowAsync(CancellationToken token)
        {
            await UITransition.RunAsync(EffectAmount, 0, value => EffectAmount = value, showDuration, token,
                showEase == Ease.NotSet ? Ease.Linear : showEase);
        }

        /// <summary>
        ///  禁用脚本时，取消监听UI面板的异步显示事件
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (uiBase != null)
            {
                uiBase.ShowAsyncActions -= OnUIShowAsync;
                uiBase.HideAsyncActions -= OnUIHideAsync;
                uiBase.ShowActions -= OnUIShow;
                uiBase.HideActions -= OnUIHide;
            }
        }

        /// <summary>
        ///  当动效进度改变时
        /// </summary>
        /// <param name="value">动效进度,0==显示，1==隐藏</param>
        protected virtual void OnEffectAmountChanged(float value)
        {
        }

        protected virtual void OnValidate()
        {
            uiBase = GetComponent<UIBase>();
            canvasGroup = GetComponent<CanvasGroup>();
            EffectAmount = effectAmount;
        }
    }
}

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
        private CancellationToken _effectLifetimeToken;
        private bool _isDestroyed;

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

        protected float ShowDuration => showDuration;
        protected float HideDuration => hideDuration;

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
        ///  CanvasGroup组件,用来控制UI面板的透明度
        /// </summary>
        [SerializeField, HideInInspector] protected CanvasGroup canvasGroup;

        /// <summary>
        ///  获取CanvasGroup组件
        /// </summary>
        protected CanvasGroup CanvasGroup
        {
            get
            {
                // Unity 已销毁对象也按缺失引用处理，补查结果写回缓存。
                if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
                if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
                return canvasGroup;
            }
        }

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
            }
        }

        /// <summary>
        ///  激活脚本时，监听UI面板的异步显示事件，加入FadeIn效果的动画
        /// </summary>
        protected virtual void Awake()
        {
            // 保留编辑器已缓存的引用；运行时 AddComponent 或缺失缓存时只补查一次。
            if (!uiBase) uiBase = GetComponent<UIBase>();
            _ = CanvasGroup;
            // 提前缓存；销毁后不得再读取 MonoBehaviour 的原生属性。
            _effectLifetimeToken = destroyCancellationToken;
            uiBase.ShowAsyncActions += ExecuteShowAsync;
            uiBase.HideAsyncActions += ExecuteHideAsync;
            uiBase.ShowActions += ExecuteShow;
            uiBase.HideActions += ExecuteHide;
        }

        private void ExecuteShow()
        {
            // 委托快照可能仍包含已在同轮较早回调中移除的动效。
            if (_isDestroyed || !this || _effectLifetimeToken.IsCancellationRequested) return;
            OnUIShow();
        }

        private void ExecuteHide()
        {
            if (_isDestroyed || !this || _effectLifetimeToken.IsCancellationRequested) return;
            OnUIHide();
        }

        private UniTask ExecuteShowAsync(CancellationToken token) => ExecuteEffectAsync(true, token);
        private UniTask ExecuteHideAsync(CancellationToken token) => ExecuteEffectAsync(false, token);

        private async UniTask ExecuteEffectAsync(bool showing, CancellationToken operationToken)
        {
            operationToken.ThrowIfCancellationRequested();
            if (_isDestroyed || !this || _effectLifetimeToken.IsCancellationRequested) return;

            // 包装整个虚方法，让派生控制器的准备与收尾也受同一寿命约束。
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(operationToken, _effectLifetimeToken);
            try
            {
                if (showing) await OnUIShowAsync(linked.Token);
                else await OnUIHideAsync(linked.Token);
            }
            catch (OperationCanceledException) when (
                _effectLifetimeToken.IsCancellationRequested && !operationToken.IsCancellationRequested)
            {
                // 单独移除动效只结束该行为，不补终点，也不取消仍存活的面板。
            }

            // 外部取消、操作替换和面板销毁始终保留 UI 操作的取消语义。
            operationToken.ThrowIfCancellationRequested();
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
        ///  销毁时解除监听；自身销毁令牌同时停止正在执行的动效。
        /// </summary>
        protected virtual void OnDestroy()
        {
            _isDestroyed = true;
            if (uiBase != null)
            {
                uiBase.ShowAsyncActions -= ExecuteShowAsync;
                uiBase.HideAsyncActions -= ExecuteHideAsync;
                uiBase.ShowActions -= ExecuteShow;
                uiBase.HideActions -= ExecuteHide;
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

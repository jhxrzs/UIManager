using System.Threading;
using UnityEngine;

namespace JH
{
    /// <summary>
    ///  利用顶点偏移和UV流动来实现的隐藏效果
    /// </summary>
    [RequireComponent(typeof(UIBase))]
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(VertexUVFlowController))]
    public class VertexAndUVFlowHide : MonoBehaviour
    {
        /// <summary>  
        ///  需要逐渐隐藏的UI面板
        /// </summary>
        [SerializeField, HideInInspector] protected UIBase uiBase;

        /// <summary>
        ///  逐渐隐藏的时间
        /// </summary>
        [SerializeField] protected float duration = 1f;

        /// <summary>
        ///  动画曲线
        /// </summary>
        [SerializeField] protected Ease tweenEase = Ease.OutExpo;

        /// <summary>
        ///  动效控制组件
        /// </summary>
        [SerializeField, HideInInspector] private VertexUVFlowController vertexUVFlowsController;

        /// <summary>
        ///  canvasGroup，来避免在隐藏时，点击事件穿透
        /// </summary>
        [SerializeField, HideInInspector] private CanvasGroup canvasGroup;


        /// <summary>
        ///  目标值
        /// </summary>
        protected virtual float EndEffectAmount => 1;

        private void OnValidate()
        {
            uiBase = GetComponent<UIBase>();
            canvasGroup = GetComponent<CanvasGroup>();
            vertexUVFlowsController = GetComponent<VertexUVFlowController>();
        }

        /// <summary>
        ///  激活脚本时，监听UI面板的异步隐藏事件，加入效果的动画
        /// </summary>
        protected virtual void Awake()
        {
            uiBase.HideAsyncActions += TweenEffectAmountAsync;
        }

        /// <summary>
        ///  禁用脚本时，取消监听UI面板的异步隐藏事件
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (uiBase != null) uiBase.HideAsyncActions -= TweenEffectAmountAsync;
        }

        /// <summary>
        ///   动画效果
        /// </summary>
        protected async JTask TweenEffectAmountAsync(CancellationToken token)
        {
            canvasGroup.interactable = canvasGroup.blocksRaycasts = EndEffectAmount == 0f;
            vertexUVFlowsController.Duration = duration;
            await JTween.Tween(vertexUVFlowsController.EffectAmount, EndEffectAmount,
                value => { vertexUVFlowsController.EffectAmount = value; }, duration, token, tweenEase);
        }
    }
}
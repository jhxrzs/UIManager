using UnityEngine;

namespace JH
{
    /// <summary>
    ///  利用顶点偏移和UV流动来实现的隐藏效果
    /// </summary>
    [RequireComponent(typeof(UIBase))]
    [RequireComponent(typeof(CanvasGroup))]
    public class VertexAndUVHide : MonoBehaviour
    {
        /// <summary>  
        ///  需要逐渐隐藏的UI面板
        /// </summary>
        [SerializeField, HideInInspector] protected UIBase uiBase;

        /// <summary>
        ///  canvasGroup，来避免在隐藏时，点击事件穿透
        /// </summary>
        [SerializeField, HideInInspector] private CanvasGroup canvasGroup;

        /// <summary>
        ///   目标值
        /// </summary>
        protected virtual float EndEffectAmount => 1;

        /// <summary>
        ///  全部的动效控制组件
        /// </summary>
        private VertexUVFlow[] VertexUVFlows => GetComponentsInChildren<VertexUVFlow>();

        private void OnValidate()
        {
            uiBase = GetComponent<UIBase>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        /// <summary>
        ///  激活脚本时，监听UI面板的异步隐藏事件，加入效果的动画
        /// </summary>
        protected virtual void Awake()
        {
            uiBase.HideActions += TweenEffectAmount;
        }

        /// <summary>
        ///  禁用脚本时，取消监听UI面板的异步隐藏事件
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (uiBase != null) uiBase.HideActions -= TweenEffectAmount;
        }

        /// <summary>
        ///   动画效果
        /// </summary>
        protected void TweenEffectAmount()
        {
            canvasGroup.interactable = canvasGroup.blocksRaycasts = EndEffectAmount == 0f;
            foreach (var vertexUVFlow in VertexUVFlows)
            {
                vertexUVFlow.EffectAmount = EndEffectAmount;
            }
        }
    }
}
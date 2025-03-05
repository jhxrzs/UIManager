using UnityEngine;

namespace JH
{
    /// <summary>
    ///  利用canvasGroup的alpha值来显示UI
    /// </summary>
    [RequireComponent(typeof(UIBase))]
    [RequireComponent(typeof(CanvasGroup))]
    public class OpaqueOnShow : MonoBehaviour
    {
        /// <summary>  
        ///  需要显示的UI面板
        /// </summary>
        [SerializeField, HideInInspector] protected UIBase uiBase;

        /// <summary>
        ///  CanvasGroup组件,用来控制UI面板的透明度
        /// </summary>
        [SerializeField, HideInInspector] protected CanvasGroup canvasGroup;

        /// <summary>
        ///  目标值：true为显示，false为隐藏
        /// </summary>
        protected virtual float TargetValue => 1;

        private void OnValidate()
        {
            uiBase = GetComponent<UIBase>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        /// <summary>
        ///  激活脚本时，监听UI面板的显示事件
        /// </summary>
        protected virtual void Awake()
        {
            uiBase.ShowActions += SetAlphaAction;
        }

        /// <summary>
        ///  禁用脚本时，取消监听UI面板的显示事件
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (uiBase != null) uiBase.ShowActions -= SetAlphaAction;
        }

        /// <summary>
        ///  设置透明度为目标值
        /// </summary>
        protected void SetAlphaAction()
        {
            canvasGroup.alpha = TargetValue;
            canvasGroup.blocksRaycasts = Mathf.Approximately(TargetValue, 1f);
        }
    }
}
using UnityEngine;

namespace JH
{
    /// <summary>
    ///  利用RectTransform的DoTween动画来显示UI的同步效果
    /// </summary>
    [RequireComponent(typeof(UIBase))]
    public class RectTransformShow : MonoBehaviour
    {
        /// <summary>  
        ///  需要显示的UI面板
        /// </summary>
        [SerializeField, HideInInspector] protected UIBase uiBase;

        /// <summary>
        ///  目标位置
        /// </summary>
        [SerializeField] private RectTransform target;

        /// <summary>
        ///   RectTransform组件
        /// </summary>
        [SerializeField, HideInInspector] protected RectTransform rectTransform;

        private void OnValidate()
        {
            uiBase = GetComponent<UIBase>();
            rectTransform = GetComponent<RectTransform>();

            // 尝试获取目标位置
            FuzzyFindTransformTool.TryGetTarget(this);
        }

        /// <summary>
        ///  激活脚本时，监听UI面板的同步显示事件
        /// </summary>
        protected virtual void Awake()
        {
            uiBase.ShowActions += SetRectTransform;
        }

        /// <summary>
        ///  禁用脚本时，取消监听UI面板的同步显示事件
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (uiBase != null) uiBase.ShowActions -= SetRectTransform;
        }

        /// <summary>
        ///   FadeIn效果
        /// </summary>
        protected void SetRectTransform()
        {
            // 完成后，设置为目标位置
            rectTransform.anchoredPosition = target.anchoredPosition;
            rectTransform.localScale = target.localScale;
            rectTransform.localEulerAngles = target.localEulerAngles;
            rectTransform.sizeDelta = target.sizeDelta;
            rectTransform.pivot = target.pivot;
        }
    }
}
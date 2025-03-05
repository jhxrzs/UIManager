namespace JH
{
    /// <summary>
    ///  利用CanvasGroup的alpha属性来隐藏UI的透明效果
    /// </summary>
    public class TransparentOnHide : OpaqueOnShow
    {
        /// <summary>
        ///  目标值：true为显示，false为隐藏
        /// </summary>
        protected override float TargetValue => 0;

        /// <summary>
        ///  激活脚本时，监听UI面板的显示事件
        /// </summary>
        protected override void Awake()
        {
            uiBase.HideActions += SetAlphaAction;
        }

        /// <summary>
        ///  禁用脚本时，取消监听UI面板的显示事件
        /// </summary>
        protected override void OnDestroy()
        {
            if (uiBase != null) uiBase.HideActions -= SetAlphaAction;
        }
    }
}
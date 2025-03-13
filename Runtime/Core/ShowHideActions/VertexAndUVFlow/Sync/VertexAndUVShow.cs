namespace JH
{
    public class VertexAndUVShow : VertexAndUVHide
    {
        protected override float EndEffectAmount => 0;
        
        /// <summary>
        ///  激活脚本时，监听UI面板的异步显示事件，加入效果的动画
        /// </summary>
        protected override void Awake()
        {
            uiBase.ShowActions += TweenEffectAmount;
        }

        /// <summary>
        ///  禁用脚本时，取消监听UI面板的异步显示事件
        /// </summary>
        protected override void OnDestroy()
        {
            if (uiBase != null) uiBase.ShowActions -= TweenEffectAmount;
        }
    }
}
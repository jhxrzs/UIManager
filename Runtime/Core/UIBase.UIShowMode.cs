namespace JH
{
    /// <summary>UI面板的分部定义，保存内部调度类型。</summary>
    public partial class UIBase
    {
        /// <summary>
        ///  当前显示请求的来源模式。
        /// </summary>
        private enum UIShowMode
        {
            /// <summary>
            ///  由公开API或序列化初态发起，并按配置策略处理连续父UI链。
            /// </summary>
            Normal,

            /// <summary>
            ///  由直接父UI发起，不再向上重复显示父UI链。
            /// </summary>
            ParentCascade
        }
    }
}

namespace JH
{
    /// <summary>
    ///  外部直接显示UI时处理连续父UI链的策略。
    /// </summary>
    public enum UIParentShowPolicy
    {
        /// <summary>
        ///  显示当前UI前自动补齐连续父UI链的显示生命周期。
        /// </summary>
        AutoShowParentChain = 0,

        /// <summary>
        ///  仅在直接父Transform挂载UIBase且连续父UI链已经完整显示时接受外部直接显示请求。
        /// </summary>
        RequireParentChainShowedForDirectShow = 1
    }
}

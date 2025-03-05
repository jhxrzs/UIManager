namespace JH
{
    /// <summary>
    ///  UI状态
    /// </summary>
    public enum UIState
    {
        /// <summary>
        ///  隐藏
        /// </summary>
        Hided,

        /// <summary>
        ///  显示
        /// </summary>
        Showed,

        /// <summary>
        /// 处于从隐藏变成显示的过程中
        /// </summary>
        Showing,

        /// <summary>
        /// 处于从显示变成隐藏的过程中
        /// </summary>
        Hiding,
    }
}
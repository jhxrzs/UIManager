namespace JH
{
    /// <summary>
    /// UI面板的基接口
    /// </summary>
    public interface IUIBase 
    {
        /// <summary>
        ///  UI状态
        /// </summary>
        UIState State { get; }
    }
}
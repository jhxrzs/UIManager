namespace JH
{
    /// <summary>
    /// 继承此接口的类，可以获取配置信息，在同一个游戏物体上，所有继承自该接口的Mono的配置信息将会保持一致
    /// </summary>
    public interface IForceSyncConfig
    {
        /// <summary>
        ///  配置信息
        /// </summary>
        public ForceSyncConfig Config { get; }
    }
    
}
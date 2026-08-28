namespace JH
{
    public partial class UIFlowGroupController
    {
        /// <summary>
        ///  子物体时间错峰使用的排序方式。
        /// </summary>
        private enum SortMode
        {
            /// <summary>
            ///  按子物体到统一终点的距离从近到远排序。
            /// </summary>
            Distance,

            /// <summary>
            ///  按子物体相对统一终点的屏幕方向顺时针排序。
            /// </summary>
            Angle
        }
    }
}

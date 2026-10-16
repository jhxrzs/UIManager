using UnityEngine;

namespace JH
{
    /// <summary>UI面板随生命周期切换的射线阻挡配置。</summary>
    public partial class UIBase
    {
        /// <summary>用于设置当前面板射线阻挡的 CanvasGroup 缓存。</summary>
        private CanvasGroup _raycastCanvasGroup;
        /// <summary>是否已保存首次读取的显示态射线阻挡配置。</summary>
        private bool _raycastSettingsInitialized;
        /// <summary>进入显示态时应恢复的射线阻挡配置。</summary>
        private bool _blocksRaycastsWhenVisible;

        /// <summary>
        /// 显示时恢复的射线阻挡值，首次初始化时取自本物体 CanvasGroup 的配置。
        /// 运行时调整此属性不会使隐藏面板开始阻挡射线，也不修改 interactable。
        /// </summary>
        public bool BlocksRaycastsWhenVisible
        {
            get
            {
                // 读取显示配置前确保 CanvasGroup 和初始缓存已经就绪。
                EnsureRaycastSettings();
                // 返回保存的显示态射线阻挡配置。
                return _blocksRaycastsWhenVisible;
            }
            set
            {
                // 设置显示配置前确保初始化完成。
                EnsureRaycastSettings();
                // 保存调用方要求的显示态射线阻挡值。
                _blocksRaycastsWhenVisible = value;
                // 仅在显示中或已显示阶段立即应用新配置。
                if (State is UIState.Showing or UIState.Showed) _raycastCanvasGroup.blocksRaycasts = value;
            }
        }

        /// <summary>补齐 CanvasGroup 缓存，并仅在首次初始化时保存显示态射线配置。</summary>
        private void EnsureRaycastSettings()
        {
            // 缓存不存在或已失效时重新取得 CanvasGroup。
            if (!_raycastCanvasGroup)
            {
                // RequireComponent 保证新节点具备 CanvasGroup；已有场景节点在此补齐。
                _raycastCanvasGroup = GetComponent<CanvasGroup>();
                // 兼容缺少组件的既有场景节点，按需补齐 CanvasGroup。
                if (!_raycastCanvasGroup) _raycastCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            // 已保存的显示配置不被后续隐藏状态覆盖。
            if (_raycastSettingsInitialized) return;

            // 必须在首次隐藏写入 false 之前保存，后续隐藏不能覆盖显示配置。
            _blocksRaycastsWhenVisible = _raycastCanvasGroup.blocksRaycasts;
            // 标记首次射线配置读取已经完成。
            _raycastSettingsInitialized = true;
        }

        /// <summary>按生命周期阶段更新射线阻挡，隐藏完成时才放行。</summary>
        /// <param name="currentState">当前生命周期状态。</param>
        private void ApplyRaycastState(UIState currentState)
        {
            // 应用生命周期状态之前确保射线配置可用。
            EnsureRaycastSettings();
            // 隐藏完成后关闭射线阻挡。
            if (currentState is UIState.Hided) _raycastCanvasGroup.blocksRaycasts = false;
            // 显示开始时恢复调用方保存的射线配置。
            else if (currentState is UIState.Showing) _raycastCanvasGroup.blocksRaycasts = _blocksRaycastsWhenVisible;
            // Showed 和 Hiding 保持现有值；全部隐藏行为完成后才放行。
        }
    }
}

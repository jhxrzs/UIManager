using UnityEngine;

namespace JH
{
    public partial class UIBase
    {
        private CanvasGroup _raycastCanvasGroup;
        private bool _raycastSettingsInitialized;
        private bool _blocksRaycastsWhenVisible;

        /// <summary>
        /// 显示时恢复的射线阻挡值，首次初始化时取自本物体 CanvasGroup 的配置。
        /// 运行时调整此属性不会使隐藏面板开始阻挡射线，也不修改 interactable。
        /// </summary>
        public bool BlocksRaycastsWhenVisible
        {
            get
            {
                EnsureRaycastSettings();
                return _blocksRaycastsWhenVisible;
            }
            set
            {
                EnsureRaycastSettings();
                _blocksRaycastsWhenVisible = value;
                if (State is UIState.Showing or UIState.Showed) _raycastCanvasGroup.blocksRaycasts = value;
            }
        }

        private void EnsureRaycastSettings()
        {
            if (!_raycastCanvasGroup)
            {
                // RequireComponent 保证新节点具备 CanvasGroup；已有场景节点在此补齐。
                _raycastCanvasGroup = GetComponent<CanvasGroup>();
                if (!_raycastCanvasGroup) _raycastCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            if (_raycastSettingsInitialized) return;

            // 必须在首次隐藏写入 false 之前保存，后续隐藏不能覆盖显示配置。
            _blocksRaycastsWhenVisible = _raycastCanvasGroup.blocksRaycasts;
            _raycastSettingsInitialized = true;
        }

        private void ApplyRaycastState(UIState currentState)
        {
            EnsureRaycastSettings();
            if (currentState is UIState.Hided) _raycastCanvasGroup.blocksRaycasts = false;
            else if (currentState is UIState.Showing) _raycastCanvasGroup.blocksRaycasts = _blocksRaycastsWhenVisible;
            // Showed 和 Hiding 保持现有值；全部隐藏行为完成后才放行。
        }
    }
}

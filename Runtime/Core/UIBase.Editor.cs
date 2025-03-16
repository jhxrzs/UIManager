#if UNITY_EDITOR
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;

namespace JH
{
    public partial class UIBase
    {
        #region 测试函数

#if ODIN_INSPECTOR
        [FoldoutGroup("测试")]
        [Button("测试显示UI")]
#endif
        [ContextMenu("测试显示UI")]
        public void TestShow()
        {
            Show();
        }
#if ODIN_INSPECTOR
        [FoldoutGroup("测试")]
        [Button("测试隐藏UI")]
#endif
        [ContextMenu("测试隐藏UI")]
        public void TestHide()
        {
            Hide();
        }
#if ODIN_INSPECTOR
        [FoldoutGroup("测试")]
        [Button("测试异步显示UI")]
#endif
        [ContextMenu("测试异步显示UI")]
        public void TestShowAsync()
        {
            _ = ShowAsync(destroyCancellationToken);
        }
#if ODIN_INSPECTOR
        [FoldoutGroup("测试")]
        [Button("测试异步隐藏UI")]
#endif
        [ContextMenu("测试异步隐藏UI")]
        public void TestHideAsync()
        {
            _ = HideAsync(destroyCancellationToken);
        }
#if ODIN_INSPECTOR
        [FoldoutGroup("测试")]
        [Button("测试切换UI")]
#endif
        [ContextMenu("测试切换UI")]
        public void TestSwitch(bool isShow)
        {
            if (isShow)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }
#if ODIN_INSPECTOR
        [FoldoutGroup("测试")]
        [Button("测试异步切换UI")]
#endif
        [ContextMenu("测试异步切换UI")]
        public void TestSwitchAsync(bool isShow)
        {
            _ = isShow ? ShowAsync(destroyCancellationToken) : HideAsync(destroyCancellationToken);
        }

        #endregion
    }
}
#endif
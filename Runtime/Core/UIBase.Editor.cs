#if UNITY_EDITOR
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;

namespace JH
{
    /// <summary>UI面板在编辑器中的显隐测试入口。</summary>
    public partial class UIBase
    {
        #region 测试函数

        /// <summary>通过编辑器菜单提交同步显示请求。</summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("测试")]
        [Button("测试显示UI")]
#endif
        [ContextMenu("测试显示UI")]
        public void TestShow()
        {
            // 通过正式同步入口提交显示请求。
            Show();
        }
        /// <summary>通过编辑器菜单提交同步隐藏请求。</summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("测试")]
        [Button("测试隐藏UI")]
#endif
        [ContextMenu("测试隐藏UI")]
        public void TestHide()
        {
            // 通过正式同步入口提交隐藏请求。
            Hide();
        }
        /// <summary>通过编辑器菜单提交与对象销毁令牌关联的异步显示请求。</summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("测试")]
        [Button("测试异步显示UI")]
#endif
        [ContextMenu("测试异步显示UI")]
        public void TestShowAsync()
        {
            // 启动异步显示，并由对象销毁令牌负责生命周期取消。
            _ = ShowAsync(destroyCancellationToken);
        }
        /// <summary>通过编辑器菜单提交与对象销毁令牌关联的异步隐藏请求。</summary>
#if ODIN_INSPECTOR
        [FoldoutGroup("测试")]
        [Button("测试异步隐藏UI")]
#endif
        [ContextMenu("测试异步隐藏UI")]
        public void TestHideAsync()
        {
            // 启动异步隐藏，并由对象销毁令牌负责生命周期取消。
            _ = HideAsync(destroyCancellationToken);
        }
        /// <summary>通过编辑器入口按指定方向提交同步显隐请求。</summary>
        /// <param name="isShow">显示方向为真，隐藏方向为假。</param>
#if ODIN_INSPECTOR
        [FoldoutGroup("测试")]
        [Button("测试切换UI")]
#endif
        [ContextMenu("测试切换UI")]
        public void TestSwitch(bool isShow)
        {
            // 根据编辑器传入方向选择显隐请求。
            if (isShow)
            {
                // 显示方向使用正式同步显示入口。
                Show();
            }
            else
            {
                // 隐藏方向使用正式同步隐藏入口。
                Hide();
            }
        }
        /// <summary>通过编辑器入口按指定方向提交异步显隐请求。</summary>
        /// <param name="isShow">显示方向为真，隐藏方向为假。</param>
#if ODIN_INSPECTOR
        [FoldoutGroup("测试")]
        [Button("测试异步切换UI")]
#endif
        [ContextMenu("测试异步切换UI")]
        public void TestSwitchAsync(bool isShow)
        {
            // 按指定方向启动异步显隐，并关联对象销毁令牌。
            _ = isShow ? ShowAsync(destroyCancellationToken) : HideAsync(destroyCancellationToken);
        }

        #endregion
    }
}
#endif
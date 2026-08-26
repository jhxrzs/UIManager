using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JH
{
    /// <summary>
    ///  键盘按键的显隐交互实体类。
    /// </summary>
    [Serializable]
    public class KeyEntity : ShowHideInteractionEntity
    {
        /// <summary>
        ///  按键名称之间使用的空格分隔符。
        /// </summary>
        private static readonly char[] KeyNameSeparators = { ' ' };

        /// <summary>
        ///  按键触发后的显隐行为。
        /// </summary>
        [SerializeField]
        private ShowHideAction action;

        /// <summary>
        ///  由用户输入的按键名称。
        /// </summary>
        [SerializeField, Tooltip("多个按键使用空格分隔，例如：A B C LeftCtrl；空格键请输入Space")]
        private string keyNames;

        /// <summary>
        ///  编辑器解析后供运行时直接读取的按键列表。
        /// </summary>
        [SerializeField, HideInInspector]
        private List<Key> keys = new();

        /// <summary>
        ///  当前无法识别的按键名称，用于Inspector显示配置错误。
        /// </summary>
        [SerializeField, HideInInspector]
        private string invalidKeyNames;

        /// <inheritdoc />
        public override ShowHideAction Action => action;

        /// <inheritdoc />
        public override void Init(UIBase ui)
        {
            // 运行时再解析一次，兼容尚未经过新版OnValidate的旧预制体和场景。
            RefreshKeys();
        }

        /// <summary>
        ///  当前键盘中任意已配置按键按下时执行显隐行为。
        /// </summary>
        /// <param name="ui">需要执行显隐行为的UI。</param>
        /// <param name="keyboard">当前帧需要检查的键盘。</param>
        internal void ExecuteWhenPressed(UIBase ui, Keyboard keyboard)
        {
            // 按输入顺序检查已解析的按键。
            for (var i = 0; i < keys.Count; i++)
            {
                // 没有在当前帧首次按下时继续检查下一项。
                if (!keyboard[keys[i]].wasPressedThisFrame) continue;

                // 任意一个按键命中后执行一次统一显隐逻辑。
                Execute(ui);

                // 同一实体在同一帧最多执行一次，避免同时按下多个配置键时重复切换。
                return;
            }
        }

        /// <summary>
        ///  将按键名称解析为运行时使用的Key枚举列表。
        /// </summary>
        internal void RefreshKeys()
        {
            // 兼容外部序列化数据将缓存列表写为null的情况。
            keys ??= new List<Key>();

            // 每次解析都先清空旧缓存，保证字符串是唯一配置来源。
            keys.Clear();

            // 空配置表示当前实体没有触发按键，不属于错误。
            if (string.IsNullOrWhiteSpace(keyNames))
            {
                invalidKeyNames = string.Empty;
                return;
            }

            // 临时保存无法识别的名称，解析完成后一次性生成提示文本。
            var invalidNames = new List<string>();

            // 仅使用空格分隔不同按键，并允许连续空格或末尾空格。
            var splitKeyNames = keyNames.Split(KeyNameSeparators, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < splitKeyNames.Length; i++)
            {
                // 去除名称两侧残留的空白，保证枚举解析使用规范名称。
                var keyName = splitKeyNames[i].Trim();

                // 分隔符之间只有空白时直接忽略。
                if (keyName.Length == 0) continue;

                // 无法映射为有效键盘按键时记录原始名称，供编辑器明确提示。
                if (!TryParseKey(keyName, out var key))
                {
                    invalidNames.Add(keyName);
                    continue;
                }

                // 重复名称只保留第一次出现的位置。
                if (keys.Contains(key)) continue;
                keys.Add(key);
            }

            // 使用统一文本保存全部无效项，避免编辑器与运行时各自维护解析规则。
            invalidKeyNames = string.Join(", ", invalidNames);
        }

        /// <summary>
        ///  尝试将单个名称解析为有效键盘按键。
        /// </summary>
        /// <param name="keyName">去除首尾空白后的按键名称。</param>
        /// <param name="key">解析成功后的按键枚举。</param>
        /// <returns>名称能够映射为有效键盘按键时返回true。</returns>
        private static bool TryParseKey(string keyName, out Key key)
        {
            // 单个数字按照主键盘数字键解释，例如“1”对应Key.Digit1。
            if (keyName.Length == 1 && char.IsDigit(keyName[0]))
            {
                // 数字别名转换为Key枚举中的标准名称后再进入统一解析。
                keyName = "Digit" + keyName;
            }

            // 拒绝其余纯数字字符串，避免Enum.TryParse把数值误当成枚举底层值。
            if (int.TryParse(keyName, out _))
            {
                key = Key.None;
                return false;
            }

            // 枚举名称忽略大小写，因此Leftctrl可以正确映射为LeftCtrl。
            if (!Enum.TryParse(keyName, true, out key)) return false;

            // 组合枚举文本可能被TryParse错误合并为另一个已定义数值，必须核对标准名称。
            var parsedKeyName = Enum.GetName(typeof(Key), key);

            // None不代表实际按键，且输入必须精确对应一个独立的Key名称。
            return key != Key.None &&
                   string.Equals(parsedKeyName, keyName, StringComparison.OrdinalIgnoreCase);
        }
    }
}

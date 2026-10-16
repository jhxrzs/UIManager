using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JH.Test_UIManager
{
    /// <summary>验证键盘名称、别名、数字快捷写法与非法输入的解析契约。</summary>
    public partial class UIKeyNameRuntimeTests
    {

        /// <summary>通过真实 KeyEntity 初始化解析按键文本，并读取序列化后的解析结果。</summary>
        /// <param name="names">需要由 KeyEntity 解析的按键名称文本。</param>
        /// <returns>有效按键与无效名称组成的解析结果。</returns>
        private static ParsedKeyConfiguration Parse(string names)
        {
            // 从按键名称 JSON 构造真实交互实体，覆盖序列化配置的解析入口。
            var entity = JsonUtility.FromJson<KeyEntity>("{\"keyNames\":\"" + names + "\"}");
            // 不绑定实际面板，只让实体执行真实按键名称解析。
            entity.Init(null);
            // 返回“JsonUtility.FromJson<ParsedKeyConfiguration>(JsonUtility.ToJson(entity))”，交给调用方继续配置或断言。
            return JsonUtility.FromJson<ParsedKeyConfiguration>(JsonUtility.ToJson(entity));
        }

        /// <summary>验证所有声明的按键名称和别名均支持不区分大小写的解析。</summary>
        [Test]
        public void KeyNames_AllDeclaredNamesAndAliases_ShouldResolveIgnoringCase()
        {
            // 逐项检查或处理“var name in Enum.GetNames(typeof(Key))”，覆盖集合中的每个对象。
            foreach (var name in Enum.GetNames(typeof(Key)))
            {
                // 保存预期结果（expected）的基准值“(Key)Enum.Parse(typeof(Key), name)”，供后续比较或恢复。
                var expected = (Key)Enum.Parse(typeof(Key), name);
                // 根据“expected == Key.None”选择对应测试路径。
                if (expected == Key.None) continue;
                // 逐项检查或处理“var spelling in new[] { name, name.ToLowerInvariant() }”，覆盖集合中的每个对象。
                foreach (var spelling in new[] { name, name.ToLowerInvariant() })
                {
                    // 保存按键解析结果（parsed）的基准值“Parse(spelling)”，供后续比较或恢复。
                    var parsed = Parse(spelling);
                    // 确认“parsed.invalidKeyNames”没有残留无效名称。
                    Assert.That(parsed.invalidKeyNames, Is.Null.Or.Empty, spelling);
                    // 确认“parsed.keys”等于“new[] { expected }”。
                    CollectionAssert.AreEqual(new[] { expected }, parsed.keys, spelling);
                }
            }
        }

        /// <summary>验证同一物理按键的多个别名不会生成重复绑定。</summary>
        [Test]
        public void KeyNames_AliasesForSamePhysicalKey_ShouldNotDuplicateBinding()
        {
            // 保存按键解析结果（parsed）的基准值“Parse("LeftMeta LeftWindows LeftApple LeftCommand RightAlt AltGr")”，供后续比较或恢复。
            var parsed = Parse("LeftMeta LeftWindows LeftApple LeftCommand RightAlt AltGr");
            // 确认“parsed.invalidKeyNames”没有残留无效名称。
            Assert.That(parsed.invalidKeyNames, Is.Null.Or.Empty);
            // 确认“parsed.keys”等于“new[] { Key.LeftMeta, Key.RightAlt }”。
            CollectionAssert.AreEqual(new[] { Key.LeftMeta, Key.RightAlt }, parsed.keys);
        }

        /// <summary>验证单个数字别名继续映射到主键盘数字行。</summary>
        [Test]
        public void KeyNames_SingleDigitAliases_ShouldKeepMappingToNumberRow()
        {
            // 按既定范围逐项采样或处理，检查边界及重复执行行为。
            for (var digit = 0; digit <= 9; digit++)
            {
                // 保存按键解析结果（parsed）的基准值“Parse(digit.ToString())”，供后续比较或恢复。
                var parsed = Parse(digit.ToString());
                // 确认“parsed.invalidKeyNames”没有残留无效名称。
                Assert.That(parsed.invalidKeyNames, Is.Null.Or.Empty);
                // 确认“parsed.keys”等于“new[] { (Key)Enum.Parse(typeof(Key), "Digit" + digit) }”。
                CollectionAssert.AreEqual(new[] { (Key)Enum.Parse(typeof(Key), "Digit" + digit) }, parsed.keys);
            }
        }

        /// <summary>验证非法枚举值、组合文本及 None 保留为无效按键名称。</summary>
        /// <param name="name">需要被解析器拒绝并原样保留的按键名称文本。</param>
        [TestCase("A,B")]
        [TestCase("A|B")]
        [TestCase("10")]
        [TestCase("999")]
        [TestCase("-1")]
        [TestCase("None")]
        [TestCase("NotAKey")]
        public void KeyNames_InvalidOrCombinedNames_ShouldRemainRejected(string name)
        {
            // 保存按键解析结果（parsed）的基准值“Parse(name)”，供后续比较或恢复。
            var parsed = Parse(name);
            // 确认“parsed.keys”为空集合。
            Assert.That(parsed.keys, Is.Empty);
            // 确认无效名称结果保留传入的按键原文 name。
            Assert.AreEqual(name, parsed.invalidKeyNames);
        }
    }
}

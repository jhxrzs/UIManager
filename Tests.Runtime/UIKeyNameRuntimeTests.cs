using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JH.Test_UIManager
{
    public class UIKeyNameRuntimeTests
    {
        [Serializable]
        private class ParsedKeyConfiguration
        {
            public List<Key> keys;
            public string invalidKeyNames;
        }

        private static ParsedKeyConfiguration Parse(string names)
        {
            var entity = JsonUtility.FromJson<KeyEntity>("{\"keyNames\":\"" + names + "\"}");
            entity.Init(null);
            return JsonUtility.FromJson<ParsedKeyConfiguration>(JsonUtility.ToJson(entity));
        }

        [Test]
        public void KeyNames_AllDeclaredNamesAndAliases_ShouldResolveIgnoringCase()
        {
            foreach (var name in Enum.GetNames(typeof(Key)))
            {
                var expected = (Key)Enum.Parse(typeof(Key), name);
                if (expected == Key.None) continue;
                foreach (var spelling in new[] { name, name.ToLowerInvariant() })
                {
                    var parsed = Parse(spelling);
                    Assert.That(parsed.invalidKeyNames, Is.Null.Or.Empty, spelling);
                    CollectionAssert.AreEqual(new[] { expected }, parsed.keys, spelling);
                }
            }
        }

        [Test]
        public void KeyNames_AliasesForSamePhysicalKey_ShouldNotDuplicateBinding()
        {
            var parsed = Parse("LeftMeta LeftWindows LeftApple LeftCommand RightAlt AltGr");
            Assert.That(parsed.invalidKeyNames, Is.Null.Or.Empty);
            CollectionAssert.AreEqual(new[] { Key.LeftMeta, Key.RightAlt }, parsed.keys);
        }

        [Test]
        public void KeyNames_SingleDigitAliases_ShouldKeepMappingToNumberRow()
        {
            for (var digit = 0; digit <= 9; digit++)
            {
                var parsed = Parse(digit.ToString());
                Assert.That(parsed.invalidKeyNames, Is.Null.Or.Empty);
                CollectionAssert.AreEqual(new[] { (Key)Enum.Parse(typeof(Key), "Digit" + digit) }, parsed.keys);
            }
        }

        [TestCase("A,B")]
        [TestCase("A|B")]
        [TestCase("10")]
        [TestCase("999")]
        [TestCase("-1")]
        [TestCase("None")]
        [TestCase("NotAKey")]
        public void KeyNames_InvalidOrCombinedNames_ShouldRemainRejected(string name)
        {
            var parsed = Parse(name);
            Assert.That(parsed.keys, Is.Empty);
            Assert.AreEqual(name, parsed.invalidKeyNames);
        }
    }
}

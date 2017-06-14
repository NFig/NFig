using System;
using System.Collections.Generic;
using System.Linq;
using NFig.Metadata;
using NFig.Tests.Common;
using NUnit.Framework;

using Default = NFig.Metadata.DefaultValue<NFig.Tests.Common.Tier, NFig.Tests.Common.DataCenter>;
using Override = NFig.Metadata.OverrideValue<NFig.Tests.Common.Tier, NFig.Tests.Common.DataCenter>;

namespace NFig.Tests
{
    [TestFixture]
    public class BySettingTests
    {
        [Test]
        public void NullConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new BySetting<Default>(null));
            Assert.Throws<ArgumentNullException>(() => new BySetting<Override>(null));
            Assert.Throws<ArgumentNullException>(() => new BySetting<SettingMetadata>(null));
            Assert.Throws<ArgumentNullException>(() => new ListBySetting<Default>(null));
            Assert.Throws<ArgumentNullException>(() => new ListBySetting<Override>(null));
            Assert.Throws<ArgumentNullException>(() => new ListBySetting<SettingMetadata>(null));
        }

        [Test]
        public void EmptyBySetting()
        {
            var bySetting = new BySetting<Default>(Array.Empty<Default>());

            Assert.AreEqual(0, bySetting.Count);
            Assert.AreEqual(0, bySetting.Keys.Count);
            Assert.AreEqual(0, bySetting.Values.Count);

            var count = 0;

            foreach (var _ in bySetting)
                count++;

            Assert.AreEqual(0, count);

            foreach (var _ in bySetting.Keys)
                count++;

            Assert.AreEqual(0, count);

            foreach (var _ in bySetting.Values)
                count++;

            Assert.AreEqual(0, count);
        }

        [Test]
        public void EmptyListBySetting()
        {
            var listBySetting = new ListBySetting<Default>(Array.Empty<Default>());

            Assert.AreEqual(0, listBySetting.Count);
            Assert.AreEqual(0, listBySetting.Keys.Count);
            Assert.AreEqual(0, listBySetting.Values.Count);
            Assert.AreEqual(0, listBySetting.GetAllValues().Count);

            var count = 0;

            foreach (var _ in listBySetting)
                count++;

            Assert.AreEqual(0, count);

            foreach (var _ in listBySetting.Keys)
                count++;

            Assert.AreEqual(0, count);

            foreach (var _ in listBySetting.Values)
                count++;

            Assert.AreEqual(0, count);

            foreach (var _ in listBySetting.GetAllValues())
                count++;

            Assert.AreEqual(0, count);
        }

        [Test]
        public void BySettingDefaults()
        {
            var defaults = GenerateDefaults(20, false);
            var bySetting = new BySetting<Default>(defaults);
            AssertBySettingMatch(bySetting, defaults);
        }

        [Test]
        public void BySettingOverrides()
        {
            var overrides = GenerateOverrides(20, false);
            var bySetting = new BySetting<Override>(overrides);
            AssertBySettingMatch(bySetting, overrides);
        }

        [Test]
        public void BySettingMetadata()
        {
            var meta = GenerateMetadata(20);
            var bySetting = new BySetting<SettingMetadata>(meta);
            AssertBySettingMatch(bySetting, meta);
        }

        [Test]
        public void ListBySettingDefaults()
        {
            var defaults = GenerateDefaults(20, true);
            var listBySetting = new ListBySetting<Default>(defaults);
            AssertListBySettingMatch(listBySetting, defaults);
        }

        [Test]
        public void ListBySettingOverrides()
        {
            var overrides = GenerateOverrides(20, true);
            var listBySetting = new ListBySetting<Override>(overrides);
            AssertListBySettingMatch(listBySetting, overrides);
        }

        //todo: public void MergeDictionaries()

        [Test]
        public void NullSerialization()
        {
            Assert.IsNull(NFigJson.Deserialize<BySetting<Default>>("null"));
            Assert.IsNull(NFigJson.Deserialize<BySetting<Override>>("null"));
            Assert.IsNull(NFigJson.Deserialize<BySetting<SettingMetadata>>("null"));
            Assert.IsNull(NFigJson.Deserialize<ListBySetting<Default>>("null"));
            Assert.IsNull(NFigJson.Deserialize<ListBySetting<Override>>("null"));
            Assert.IsNull(NFigJson.Deserialize<ListBySetting<SettingMetadata>>("null"));
        }

        [Test]
        public void SerializeBySettingDefaults()
        {
            var defaults = GenerateDefaults(20, false);
            var bySettingOrig = new BySetting<Default>(defaults);
            var json = NFigJson.Serialize(bySettingOrig);
            var bySetting = NFigJson.Deserialize<BySetting<Default>>(json);
            AssertBySettingMatch(bySetting, defaults);
        }

        [Test]
        public void SerializeBySettingOverrides()
        {
            var overrides = GenerateOverrides(20, false);
            var bySettingOrig = new BySetting<Override>(overrides);
            var json = NFigJson.Serialize(bySettingOrig);
            var bySetting = NFigJson.Deserialize<BySetting<Override>>(json);
            AssertBySettingMatch(bySetting, overrides);
        }

        [Test]
        public void SerializeBySettingMetadata()
        {
            var meta = GenerateMetadata(20);
            var bySettingOrig = new BySetting<SettingMetadata>(meta);
            var json = NFigJson.Serialize(bySettingOrig);
            var bySetting = NFigJson.Deserialize<BySetting<SettingMetadata>>(json);
            AssertBySettingMatch(bySetting, meta);
        }

        [Test]
        public void SerializeListBySettingDefaults()
        {
            var defaults = GenerateDefaults(20, true);
            var listBySettingOrig = new ListBySetting<Default>(defaults);
            var json = NFigJson.Serialize(listBySettingOrig);
            var bySetting = NFigJson.Deserialize<ListBySetting<Default>>(json);
            AssertListBySettingMatch(bySetting, defaults);
        }

        [Test]
        public void SerializeListBySettingOverrides()
        {
            var overrides = GenerateOverrides(20, true);
            var listBySettingOrig = new ListBySetting<Override>(overrides);
            var json = NFigJson.Serialize(listBySettingOrig);
            var bySetting = NFigJson.Deserialize<ListBySetting<Override>>(json);
            AssertListBySettingMatch(bySetting, overrides);
        }

        static void AssertBySettingMatch<T>(BySetting<T> bySetting, List<T> settingValues) where T : IBySettingItem
        {
            var dict = settingValues.ToDictionary(d => d.Name);

            Assert.AreEqual(settingValues.Count, bySetting.Count);
            Assert.AreEqual(dict.Count, bySetting.Count);
            Assert.AreEqual(dict.Count, bySetting.Keys.Count);
            Assert.AreEqual(dict.Count, bySetting.Values.Count);

            foreach (var d in settingValues)
            {
                Assert.IsTrue(bySetting.ContainsKey(d.Name));
                Assert.AreEqual(d, bySetting[d.Name]);
            }

            var count = 0;
            foreach (var kvp in bySetting)
            {
                Assert.IsTrue(dict.ContainsKey(kvp.Key));
                Assert.AreEqual(kvp.Key, kvp.Value.Name);
                Assert.AreEqual(kvp.Value, dict[kvp.Key]);
                count++;
            }
            Assert.AreEqual(bySetting.Count, count);

            count = 0;
            foreach (var key in bySetting.Keys)
            {
                Assert.IsTrue(bySetting.ContainsKey(key));
                Assert.IsTrue(dict.ContainsKey(key));
                count++;
            }
            Assert.AreEqual(bySetting.Keys.Count, count);

            count = 0;
            foreach (var val in bySetting.Values)
            {
                Assert.IsTrue(bySetting.ContainsKey(val.Name));
                Assert.IsTrue(dict.ContainsKey(val.Name));
                Assert.AreEqual(dict[val.Name], bySetting[val.Name]);
                count++;
            }
            Assert.AreEqual(bySetting.Keys.Count, count);
        }

        static void AssertListBySettingMatch<T>(ListBySetting<T> listBySetting, List<T> settingValues) where T : ISettingValue<Tier, DataCenter>
        {
            var dict = settingValues.GroupBy(d => d.Name).ToDictionary(g => g.Key, g => g.ToList());

            Assert.AreEqual(dict.Count, listBySetting.Count);
            Assert.AreEqual(dict.Count, listBySetting.Keys.Count);
            Assert.AreEqual(dict.Count, listBySetting.Values.Count);
            Assert.AreEqual(settingValues.Count, listBySetting.GetAllValues().Count);

            foreach (var kvp in dict)
            {
                Assert.IsTrue(listBySetting.ContainsKey(kvp.Key));
                Assert.AreEqual(kvp.Value.Count, listBySetting[kvp.Key].Count);

                foreach (var item in kvp.Value)
                {
                    Assert.IsTrue(listBySetting[kvp.Key].Contains(item));
                }
            }

            var count = 0;
            foreach (var kvp in listBySetting)
            {
                Assert.IsTrue(listBySetting.ContainsKey(kvp.Key));
                Assert.IsTrue(dict.ContainsKey(kvp.Key));
                Assert.AreEqual(dict[kvp.Key].Count, kvp.Value.Count);
                Assert.AreEqual(listBySetting[kvp.Key].Count, kvp.Value.Count);

                var subCount = 0;
                foreach (var _ in kvp.Value)
                    subCount++;

                Assert.AreEqual(kvp.Value.Count, subCount);

                count++;
            }
            Assert.AreEqual(listBySetting.Count, count);

            count = 0;
            foreach (var key in listBySetting.Keys)
            {
                Assert.IsTrue(listBySetting.ContainsKey(key));
                Assert.IsTrue(dict.ContainsKey(key));
                Assert.AreEqual(dict[key].Count, listBySetting[key].Count);
                count++;
            }
            Assert.AreEqual(listBySetting.Keys.Count, count);

            count = 0;
            foreach (var val in listBySetting.Values)
            {
                var key = val.First().Name;
                Assert.IsTrue(listBySetting.ContainsKey(key));
                Assert.IsTrue(dict.ContainsKey(key));
                Assert.AreEqual(dict[key].Count, val.Count);
                Assert.AreEqual(listBySetting[key].Count, val.Count);

                var subCount = 0;
                foreach (var _ in val)
                    subCount++;

                Assert.AreEqual(val.Count, subCount);

                count++;
            }
            Assert.AreEqual(listBySetting.Values.Count, count);

            count = 0;
            foreach (var item in listBySetting.GetAllValues())
            {
                Assert.IsTrue(listBySetting.ContainsKey(item.Name));
                Assert.IsTrue(dict.ContainsKey(item.Name));
                Assert.IsTrue(listBySetting[item.Name].Contains(item));
                Assert.IsTrue(dict[item.Name].Contains(item));
                count++;
            }
            Assert.AreEqual(listBySetting.GetAllValues().Count, count);
        }

        static List<Default> GenerateDefaults(int keys, bool multiplePerKey)
        {
            var defaults = new List<Default>();
            for (var ki = 0; ki < keys; ki++)
            {
                var name = Generate.SettingName();

                var count = multiplePerKey ? Generate.Integer(1, 6) : 1;
                for (var ci = 0; ci < count; ci++)
                {
                    defaults.Add(RandomDefault(name));
                }
            }

            return defaults;
        }

        static List<Override> GenerateOverrides(int keys, bool multiplePerKey)
        {
            var overrides = new List<Override>();
            for (var ki = 0; ki < keys; ki++)
            {
                var name = Generate.SettingName();

                var count = multiplePerKey ? Generate.Integer(1, 6) : 1;
                for (var ci = 0; ci < count; ci++)
                {
                    overrides.Add(RandomOverride(name));
                }
            }

            return overrides;
        }

        static List<SettingMetadata> GenerateMetadata(int keys)
        {
            var meta = new List<SettingMetadata>();
            for (var i = 0; i < keys; i++)
            {
                var m = new SettingMetadata(
                    Generate.SettingName(),
                    Generate.Description(),
                    Generate.Word(),
                    Generate.Bool(),
                    Generate.Bool(),
                    Generate.Word(),
                    Generate.Bool(),
                    Generate.Bool());

                meta.Add(m);
            }

            return meta;
        }

        static Default RandomDefault(string name)
        {
            var subAppId = Generate.Bool() ? (int?)Generate.Integer(0, 20) : null;
            return new Default(name, Generate.Word(), subAppId, Generate.Tier(), Generate.DataCenter(), Generate.Bool());
        }

        static Override RandomOverride(string name)
        {
            var subAppId = Generate.Bool() ? (int?)Generate.Integer(0, 20) : null;
            var expirationTime = Generate.Bool() ? (DateTimeOffset?)DateTimeOffset.UtcNow.AddHours(Generate.Integer(1, 48)) : null;
            return new Override(name, Generate.Word(), subAppId, Generate.DataCenter(), expirationTime);
        }
    }
}
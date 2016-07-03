using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace NFig.Tests
{
    [TestFixture]
    public class OverrideTests
    {
        [Test]
        public void ValidOverrideTest()
        {
            var factory = new SettingsFactory<OverrideSettings, Tier, DataCenter>(null, null);

            var overrides = new List<SettingValue<Tier, DataCenter>>()
            {
                new SettingValue<Tier, DataCenter>("A", "10", DataCenter.Any),
                new SettingValue<Tier, DataCenter>("B", "11", DataCenter.Any),
            };

            var s = factory.GetAppSettings(Tier.Local, DataCenter.Local, overrides);

            Assert.AreEqual(s.A, 10);
            Assert.AreEqual(s.B, 11);
            Assert.AreEqual(s.C, 2);
        }

        [Test]
        public void InvalidOverrideTest()
        {
            var factory = new SettingsFactory<OverrideSettings, Tier, DataCenter>(null, null);

            var overrides = new List<SettingValue<Tier, DataCenter>>()
            {
                new SettingValue<Tier, DataCenter>("A", "a", DataCenter.Any),
                new SettingValue<Tier, DataCenter>("B", "b", DataCenter.Any),
                new SettingValue<Tier, DataCenter>("C", "12", DataCenter.Any),
            };

            OverrideSettings s;
            var invalidOverrides = factory.TryGetAppSettings(out s, Tier.Local, DataCenter.Local, overrides);
            Console.WriteLine(invalidOverrides.Message);

            Assert.True(invalidOverrides != null && invalidOverrides.Exceptions.Count == 2);

            var ex = invalidOverrides.Exceptions[0];
            Assert.True(ex.IsOverride);
            Assert.True(ex.SettingName == "A");

            Assert.AreEqual(s.A, 0);
            Assert.AreEqual(s.B, 1);
            Assert.AreEqual(s.C, 12);
        }

        private class OverrideSettings : SettingsBase
        {
            [Setting(0)]
            public int A { get; private set; }

            [Setting(1)]
            public int B { get; private set; }

            [Setting(2)]
            public int C { get; private set; }
        }
    }
}
using System;
using System.Collections.Generic;
using NUnit.Framework;

using OverrideValue = NFig.OverrideValue<NFig.Tests.SubApp, NFig.Tests.Tier, NFig.Tests.DataCenter>;

namespace NFig.Tests.FactoryTests
{
    [TestFixture]
    public class OverrideTests
    {
        [Test]
        public void ValidOverrideTest()
        {
            var factory = Utils.CreateFactory<OverrideSettings>();

            var overrides = new List<OverrideValue>()
            {
                new OverrideValue("A", "10", SubApp.Global, DataCenter.Any),
                new OverrideValue("B", "11", SubApp.Global, DataCenter.Any),
            };

            var snapshot = Utils.CreateSnapshot(overrides: overrides);
            var s = factory.GetSettings(snapshot);

            Assert.AreEqual(s.A, 10);
            Assert.AreEqual(s.B, 11);
            Assert.AreEqual(s.C, 2);
        }

        [Test]
        public void InvalidOverrideTest()
        {
            var factory = Utils.CreateFactory<OverrideSettings>();

            var overrides = new List<OverrideValue>()
            {
                new OverrideValue("A", "a", SubApp.Global, DataCenter.Any),
                new OverrideValue("B", "b", SubApp.Global, DataCenter.Any),
                new OverrideValue("C", "12", SubApp.Global, DataCenter.Any),
            };

            var snapshot = Utils.CreateSnapshot(overrides: overrides);

            OverrideSettings s;
            var invalidOverrides = factory.TryGetSettingsForGlobalApp(out s, snapshot);
            Console.WriteLine(invalidOverrides.Message);

            Assert.True(invalidOverrides != null && invalidOverrides.Exceptions.Count == 2);

            var ex = invalidOverrides.Exceptions[0];
            Assert.True(ex.IsOverride);
            Assert.True(ex.SettingName == "A");

            Assert.AreEqual(s.A, 0);
            Assert.AreEqual(s.B, 1);
            Assert.AreEqual(s.C, 12);
        }

        class OverrideSettings : SettingsBase
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
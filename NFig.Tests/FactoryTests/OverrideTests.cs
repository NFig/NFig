﻿using System;
using System.Collections.Generic;
using NFig.Metadata;
using NFig.Tests.Common;
using NUnit.Framework;

using OverrideValue = NFig.Metadata.OverrideValue<NFig.Tests.Common.Tier, NFig.Tests.Common.DataCenter>;

namespace NFig.Tests.FactoryTests
{
    [TestFixture]
    public class OverrideTests
    {
        [Test]
        public void ValidOverrideTest()
        {
            var factory = Utils.CreateFactory<OverrideSettings>();

            var overrides = new ListBySetting<OverrideValue>(
                new[]
                {
                    new OverrideValue<Tier, DataCenter>("A", "10", null, DataCenter.Any, null),
                    new OverrideValue("B", "11", null, DataCenter.Any, null),
                });

            var snapshot = Utils.CreateSnapshot(overrides: overrides);
            var s = factory.GetSettings(snapshot: snapshot);

            Assert.AreEqual(s.A, 10);
            Assert.AreEqual(s.B, 11);
            Assert.AreEqual(s.C, 2);
        }

        [Test]
        public void InvalidOverrideTest()
        {
            var factory = Utils.CreateFactory<OverrideSettings>();

            var overrides = new ListBySetting<OverrideValue>(
                new[]
                {
                    new OverrideValue("A", "a", null, DataCenter.Any, null),
                    new OverrideValue("B", "b", null, DataCenter.Any, null),
                    new OverrideValue("C", "12", null, DataCenter.Any, null),
                });

            var snapshot = Utils.CreateSnapshot(overrides: overrides);

            List<InvalidOverrideValueException> invalidOverrides = null;
            factory.TryGetSettings(null, snapshot, out var s, ref invalidOverrides);

            Assert.True(invalidOverrides != null);
            Assert.True(invalidOverrides.Count == 2);

            Assert.True(invalidOverrides[0].SettingName == "A");
            Assert.True(invalidOverrides[1].SettingName == "B");

            Assert.AreEqual(s.A, 0);
            Assert.AreEqual(s.B, 1);
            Assert.AreEqual(s.C, 12);
        }

        class OverrideSettings : SettingsBase
        {
            [Setting(0)]
            public int A { get; }

            [Setting(1)]
            public int B { get; }

            [Setting(2)]
            public int C { get; }
        }
    }
}
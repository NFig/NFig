using System;
using System.Collections.Generic;
using NUnit.Framework;

using SettingValue = NFig.SettingValue<NFig.Tests.SubApp, NFig.Tests.Tier, NFig.Tests.DataCenter>;

namespace NFig.Tests.FactoryTests
{
    [TestFixture]
    public class GetterOnlyTests
    {
        [Test]
        public void GetterOnly()
        {
            var factory = Utils.CreateFactory<GetterOnlySettings>();

            var overrides = new List<SettingValue>()
            {
                SettingValue.CreateOverrideValue("One.A", "10", SubApp.Global, DataCenter.Any),
                SettingValue.CreateOverrideValue("Two.B.C", "11", SubApp.Global, DataCenter.Any),
            };

            var snapshot = Utils.CreateSnapshot(overrides: overrides);
            var s = factory.GetSettings(snapshot);

            Assert.AreEqual(s.One.A, 10);
            Assert.AreEqual(s.Two.B.C, 11);
        }

        [Test]

        public void NoGetterThrows()
        {
            Assert.Throws<NFigException>(() =>
            {
                var factory = Utils.CreateFactory<NoSetterTraditionalSettings>();
                var snapshot = Utils.CreateSnapshot();

                factory.GetSettings(snapshot);
            });

            Assert.Throws<NFigException>(() =>
            {
                var factory = Utils.CreateFactory<NoSetterExpressionSettings>();
                var snapshot = Utils.CreateSnapshot();

                factory.GetSettings(snapshot);
            });
        }

        class NoSetterTraditionalSettings : SettingsBase
        {
            private string _nope;

            [Setting("Test")]
            public string NopeTraditional
            {
                get { return _nope; }
            }
        }

        class NoSetterExpressionSettings : SettingsBase
        {
            private string _nope;

            [Setting("Test")]
            public string NopeExpression => _nope;
        }

        class GetterOnlySettings : SettingsBase
        {
            [SettingsGroup]
            public OneSettings One { get; }

            public class OneSettings
            {
                [Setting(2)]
                public int A { get; }
            }

            [SettingsGroup]
            public TwoSettings Two { get; }

            public class TwoSettings
            {
                [SettingsGroup]
                public BSettings B { get; }

                public class BSettings
                {
                    [Setting(3)]
                    public int C { get; }
                }
            }
        }
    }
}
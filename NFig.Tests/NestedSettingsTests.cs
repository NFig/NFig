using NUnit.Framework;

namespace NFig.Tests
{
    [TestFixture]
    public class NestedSettingsTests
    {
        [Test]
        public void NestedSettingsTest()
        {
            var factory = new SettingsFactory<NestedSettings, Tier, DataCenter>(Tier.Local, null, null);
            var s = factory.GetAppSettings(DataCenter.Local);

            Assert.AreEqual(s.One.A, 2);
            Assert.AreEqual(s.Two.B.C, 3);
        }

        private class NestedSettings : SettingsBase
        {
            [SettingsGroup]
            public OneSettings One { get; private set; }

            public class OneSettings
            {
                [Setting(2)]
                public int A { get; private set; }
            }

            [SettingsGroup]
            public TwoSettings Two { get; private set; }

            public class TwoSettings
            {
                [SettingsGroup]
                public BSettings B { get; private set; }

                public class BSettings
                {
                    [Setting(3)]
                    public int C { get; private set; }
                }
            }
        }
    }
}
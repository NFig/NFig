﻿using NUnit.Framework;

namespace NFig.Tests
{
    [TestFixture]
    public class EnumTests
    {
        [Test]
        public void EnumTest()
        {
            var factory = new SettingsFactory<EnumSettings, Tier, DataCenter>(Tier.Local, null, null);
            var s = factory.GetAppSettings(DataCenter.Local);

            Assert.True(s.First == TestEnum.Zero, "First");
            Assert.True(s.Second == TestEnum.One, "Second");
            Assert.True(s.Third == TestEnum.Two, "Third");
            Assert.True(s.Fourth == TestEnum.Three, "Fourth");
        }

        public enum TestEnum
        {
            Zero = 0,
            One,
            Two,
            Three,
        }

        private class EnumSettings : SettingsBase
        {
            [Setting(TestEnum.Zero)]
            public TestEnum First { get; private set; }

            [Setting("1")]
            public TestEnum Second { get; private set; }

            [Setting("2")]
            public TestEnum Third { get; private set; }

            [Setting(TestEnum.Three)]
            public TestEnum Fourth { get; private set; }
        }
    }
}
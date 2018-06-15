using System.Linq;
using NUnit.Framework;

namespace NFig.Tests
{
    [TestFixture]
    public class CustomConverterTests
    {
        [Test]
        public void CustomConverterTest()
        {
            var factory = new SettingsFactory<CustomConverterSettings, Tier, DataCenter>();
            var s = factory.GetAppSettings(Tier.Local, DataCenter.Local);

            Assert.True(s.Ints != null, "Ints should not be null");
            Assert.True(s.Ints.Length == 3, "Ints should have length of 3, but is length " + s.Ints.Length);
            Assert.AreEqual(2, s.Ints[0]);
            Assert.AreEqual(3, s.Ints[1]);
            Assert.AreEqual(4, s.Ints[2]);

            Assert.AreEqual(3, s.OffByOne);
            Assert.AreEqual(7, s.NotOffByOne);
            Assert.AreEqual(1.8, s.Number);
        }

        [Test]
        public void AdditionalDefaultConvertersTest()
        {
            var additionalDefaultConverters = new ISettingConverter[] { new OffByOneConverter(), new IntArrayConverter() };
            var factory = new SettingsFactory<DefaultConverterSettings, Tier, DataCenter>(additionalDefaultConverters);
            var s = factory.GetAppSettings(Tier.Local, DataCenter.Local);

            Assert.True(s.Ints != null, "Ints should not be null");
            Assert.True(s.Ints.Length == 3, "Ints should have length of 3, but is length " + s.Ints.Length);
            Assert.AreEqual(3, s.Ints[0]);
            Assert.AreEqual(4, s.Ints[1]);
            Assert.AreEqual(5, s.Ints[2]);

            Assert.AreEqual(18, s.OffByOne);
            Assert.AreEqual(1.7, s.Number);
        }

        private class CustomConverterSettings : SettingsBase
        {
            [Setting("2,3,4")]
            [SettingConverter(typeof(IntArrayConverter))]
            public int[] Ints { get; private set; }

            [Setting("2")]
            [SettingConverter(typeof(OffByOneConverter))]
            public int OffByOne { get; private set; }

            [Setting("7")]
            public int NotOffByOne { get; private set; }

            [Setting(1.8)]
            public double Number { get; private set; }
        }

        private class DefaultConverterSettings : SettingsBase
        {
            [Setting("3,4,5")]
            public int[] Ints { get; private set; }

            [Setting("17")]
            public int OffByOne { get; private set; }

            [Setting(1.7)]
            public double Number { get; private set; }
        }

        public class IntArrayConverter : ISettingConverter<int[]>
        {
            public string GetString(int[] value)
            {
                return string.Join(",", value);
            }

            public int[] GetValue(string str)
            {
                return str.Split(',').Select(int.Parse).ToArray();
            }
        }

        public class OffByOneConverter : ISettingConverter<int>
        {
            public string GetString(int value) => (value - 1).ToString();
            public int GetValue(string str) => int.Parse(str) + 1;
        }
    }
}
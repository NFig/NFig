using System;
using System.Linq;
using NFig.Converters;
using NFig.Tests.Common;
using NUnit.Framework;

namespace NFig.Tests.FactoryTests
{
    [TestFixture]
    public class CustomConverterTests
    {
        [Test]
        public void CustomConverterTest()
        {
            var factory = Utils.CreateFactory<CustomConverterSettings>();
            var s = factory.GetSettings();

            Assert.True(s.Ints != null, "Ints should not be null");
            Assert.True(s.Ints.Length == 3, "Ints should have length of 3, but is length " + s.Ints.Length);
            Assert.AreEqual(2, s.Ints[0]);
            Assert.AreEqual(3, s.Ints[1]);
            Assert.AreEqual(4, s.Ints[2]);

            Assert.AreEqual(3, s.Regular);
            Assert.AreEqual(4, s.OffByOne);

            Assert.AreEqual(2, s.NestedOne.OffByOne);
            Assert.AreEqual(3, s.NestedOne.OffByTwo);

            Assert.AreEqual(3, s.NestedOne.OffByOne);
            Assert.AreEqual(4, s.NestedOne.OffByTwo);
        }

        [Test]
        public void MissingConverterTest()
        {
            Assert.Throws<InvalidSettingConverterException>(() => Utils.CreateFactory<MissingConverterSettings>());
        }

        class CustomConverterSettings : SettingsBase
        {
            [Setting("2,3,4")]
            [SettingConverter(typeof(IntArrayConverter))]
            public int[] Ints { get; }

            [Setting("3")]
            public int Regular { get; }

            [Setting("3")]
            [SettingConverter(typeof(OffByOneConverter))]
            public int OffByOne { get; }

            [SettingsGroup]
            [SettingConverter(typeof(OffByOneConverter))]
            public NestedOneSettings NestedOne { get; }

            public class NestedOneSettings
            {
                [Setting("1")]
                public int OffByOne { get; }

                [Setting("1")]
                [SettingConverter(typeof(OffByTwoConverter))]
                public int OffByTwo { get; }
            }

            [SettingsGroup]
            public NestedTwoSettings NestedTwo { get; }

            [SettingConverter(typeof(OffByTwoConverter))]
            public class NestedTwoSettings
            {
                [Setting("2")]
                [SettingConverter(typeof(OffByOneConverter))]
                public int OffByOne { get; }

                [Setting("2")]
                public int OffByTwo { get; }
            }
        }

        [SettingConverter(typeof(IntArrayConverter))]
        [SettingConverter(typeof(OffByOneConverter))]
        class TopLevelConvertersSettings : SettingsBase
        {
            [Setting("5,6,7")]
            public int[] Ints { get; }
        }

        class MissingConverterSettings : SettingsBase
        {
            [Setting("1,2,3")]
            public int[] Ints { get; }
        }

        public class IntArrayConverter : ISettingConverter<int[]>
        {
            public string GetString(int[] value) => value == null ? null : string.Join(",", value);
            public int[] GetValue(string str) => str?.Split(',').Select(s => int.Parse(s)).ToArray();
        }

        public class OffByOneConverter : ISettingConverter<int>
        {
            public string GetString(int value) => (value - 1).ToString();
            public int GetValue(string str) => int.Parse(str) + 1;
        }

        public class OffByTwoConverter : ISettingConverter<int>
        {
            public string GetString(int value) => (value - 2).ToString();
            public int GetValue(string str) => int.Parse(str) + 2;
        }
    }
}
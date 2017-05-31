using NFig.Tests.Common;
using NUnit.Framework;

namespace NFig.Tests.FactoryTests
{
    [TestFixture]
    public class SubAppTests
    {
        [Test]
        public void IdenticalSubApp()
        {
            var factory = Utils.CreateFactory<SubAppSettings>();

            var rootSettings = factory.GetSettings();
            Assert.AreEqual(1, rootSettings.One);
            Assert.AreEqual(2, rootSettings.Two);
            Assert.AreEqual(3, rootSettings.Three);
            Assert.AreEqual(4, rootSettings.Four);

            var zeroSettings = factory.GetSettings(0, "Zero");
            Assert.AreEqual(1, zeroSettings.One);
            Assert.AreEqual(10, zeroSettings.Two);
            Assert.AreEqual(11, zeroSettings.Three);
            Assert.AreEqual(4, zeroSettings.Four);

            var oneSettings = factory.GetSettings(1, "One");
            Assert.AreEqual(1, oneSettings.One);
            Assert.AreEqual(2, oneSettings.Two);
            Assert.AreEqual(12, oneSettings.Three);
            Assert.AreEqual(13, oneSettings.Four);

            var twoSettings = factory.GetSettings(2, "Two");
            Assert.AreEqual(1, twoSettings.One);
            Assert.AreEqual(2, twoSettings.Two);
            Assert.AreEqual(3, twoSettings.Three);
            Assert.AreEqual(4, twoSettings.Four);
        }

        public void SubAppDefaults()
        {
            //
        }

        public void SubAppOverrides()
        {
            //
        }

        class SubAppSettings : SettingsBase
        {
            [Setting(1)]
            public int One { get; }

            [Setting(2)]
            [SubApp(0, 10)]
            public int Two { get; }

            [Setting(3)]
            [SubApp(0, 11)]
            [SubApp(1, 12)]
            public int Three { get; }

            [Setting(4)]
            [NamedSubApp("One", 13)]
            public int Four { get; }
        }
    }
}
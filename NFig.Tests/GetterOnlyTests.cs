using NUnit.Framework;

namespace NFig.Tests
{
    [TestFixture]
    public class GetterOnlyTests
    {
        [Test]
        public void GetterOnly()
        {
            var store = Utils.CreateStore<GetterOnlySettings>();

            var settings = store.GetSettingsForGlobalApp();

            Assert.AreEqual(settings.A, 2);
            Assert.AreEqual(settings.One.B, 3);

            store.SetOverride("A", "10", DataCenter.Any, "user");
            store.SetOverride("One.B", "11", DataCenter.Any, "user");

            settings = store.GetSettingsForGlobalApp();

            Assert.AreEqual(settings.A, 10);
            Assert.AreEqual(settings.One.B, 11);
        }

        [Test]
        public void ExplicitBackingFieldThrows()
        {
            Assert.Throws<NFigException>(() => Utils.CreateStore<ExplicitBackingFieldSettings>());
        }

        class ExplicitBackingFieldSettings : SettingsBase
        {
#pragma warning disable 649
            string _explicitBackingField;
#pragma warning restore 649

            [Setting("Test")]
            public string ExplicitBackingFieldExpression => _explicitBackingField;
        }

        class GetterOnlySettings : SettingsBase
        {
            [Setting(2)]
            public int A { get; }

            [SettingsGroup]
            public OneSettings One { get; }

            public class OneSettings
            {
                [Setting(3)]
                public int B { get; }
            }
        }
    }
}
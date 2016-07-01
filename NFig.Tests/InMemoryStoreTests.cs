using NUnit.Framework;

namespace NFig.Tests
{
    public class InMemoryStoreTests
    {
        private const string AppName = "TestApp";

        [Test]
        public void Defaults()
        {
            var store = new NFigMemoryStore<InMemorySettings, Tier, DataCenter>(Tier.Local, DataCenter.East);
            var settings = store.GetAppSettings(AppName);
            Assert.AreEqual(23, settings.TopInteger);
            Assert.AreEqual("Twenty-Three", settings.TopString);
            Assert.AreEqual(17, settings.Nested.Integer);
            Assert.AreEqual("Seventeen", settings.Nested.String);

            store = new NFigMemoryStore<InMemorySettings, Tier, DataCenter>(Tier.Prod, DataCenter.East);
            settings = store.GetAppSettings(AppName);
            Assert.AreEqual(23, settings.TopInteger);
            Assert.AreEqual("Twenty-Three", settings.TopString);
            Assert.AreEqual(7, settings.Nested.Integer);
            Assert.AreEqual("Seven", settings.Nested.String);
        }

        [Test]
        public void Overrides()
        {
            var store = new NFigMemoryStore<InMemorySettings, Tier, DataCenter>(Tier.Local, DataCenter.East);

            store.SetOverride(AppName, "TopInteger", "3", DataCenter.Any, "Bret");
            store.SetOverride(AppName, "Nested.Integer", "7", DataCenter.East, "Bret");
            store.SetOverride(AppName, "Nested.String", "Something", DataCenter.West, "Bret");

            var settings = store.GetAppSettings(AppName);
            Assert.AreEqual(3, settings.TopInteger);
            Assert.AreEqual("Twenty-Three", settings.TopString);
            Assert.AreEqual(7, settings.Nested.Integer);
            Assert.AreEqual("Seventeen", settings.Nested.String);
        }

        [Test]
        public void SubscribeToUpdates()
        {
            var store = new NFigMemoryStore<InMemorySettings, Tier, DataCenter>(Tier.Local, DataCenter.West);

            InMemorySettings settings = null;
            var callbackCount = 0;

            store.SubscribeToAppSettings(AppName, (ex, settingsObj, storeObj) =>
            {
                if (ex != null)
                    throw ex;

                Assert.AreSame(store, storeObj);
                settings = settingsObj;
                callbackCount++;
            });
            
            Assert.AreEqual(1, callbackCount);
            Assert.IsNotNull(settings);
            Assert.IsNull(settings.Commit);

            store.SetOverride(AppName, "Nested.Integer", "32", DataCenter.Any, "Bret");

            Assert.AreEqual(2, callbackCount);
            Assert.IsNotNull(settings.Commit);
        }

        private class InMemorySettings : SettingsBase
        {
            [Setting(23)]
            public int TopInteger { get; private set; }
            [Setting("Twenty-Three")]
            public string TopString { get; private set; }

            [SettingsGroup]
            public NestedSettings Nested { get; private set; }

            public class NestedSettings
            {
                [Setting(17)]
                [Tier(Tier.Prod, 7)]
                public int Integer { get; private set; }
                [Setting("Seventeen")]
                [Tier(Tier.Prod, "Seven")]
                public string String { get; private set; }
            }
        }
    }
}
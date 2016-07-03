using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NFig.InMemory;
using NFig.Logging;
using NUnit.Framework;

namespace NFig.Tests
{
    public class InMemoryStoreTests
    {
        private const string APP_NAME = "TestApp";
        private const string USER_A = "Andrew";
        private const string USER_B = "Bret";
        private const string USER_C = "Charlie";

        [Test]
        public void Defaults()
        {
            var store = new NFigMemoryStore<InMemorySettings, Tier, DataCenter>(Tier.Local, DataCenter.East);
            var settings = store.GetAppSettings(APP_NAME);
            Assert.AreEqual(23, settings.TopInteger);
            Assert.AreEqual("Twenty-Three", settings.TopString);
            Assert.AreEqual(17, settings.Nested.Integer);
            Assert.AreEqual("Seventeen", settings.Nested.String);

            store = new NFigMemoryStore<InMemorySettings, Tier, DataCenter>(Tier.Prod, DataCenter.East);
            settings = store.GetAppSettings(APP_NAME);
            Assert.AreEqual(23, settings.TopInteger);
            Assert.AreEqual("Twenty-Three", settings.TopString);
            Assert.AreEqual(7, settings.Nested.Integer);
            Assert.AreEqual("Seven", settings.Nested.String);
        }

        [Test]
        public void Overrides()
        {
            var store = new NFigMemoryStore<InMemorySettings, Tier, DataCenter>(Tier.Local, DataCenter.East);

            store.SetOverride(APP_NAME, "TopInteger", "3", DataCenter.Any, USER_A);
            store.SetOverride(APP_NAME, "Nested.Integer", "7", DataCenter.East, USER_A);
            store.SetOverride(APP_NAME, "Nested.String", "Something", DataCenter.West, USER_A);

            var settings = store.GetAppSettings(APP_NAME);
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

            store.SubscribeToAppSettings(APP_NAME, (ex, settingsObj, storeObj) =>
            {
                if (ex != null)
                    throw ex;

                Assert.AreSame(store, storeObj);
                settings = settingsObj;
                callbackCount++;
            });
            
            Assert.AreEqual(1, callbackCount);
            Assert.IsNotNull(settings);
            Assert.AreEqual(NFigMemoryStore < InMemorySettings, Tier, DataCenter >.INITIAL_COMMIT, settings.Commit);

            store.SetOverride(APP_NAME, "Nested.Integer", "32", DataCenter.Any, USER_A);

            Assert.AreEqual(2, callbackCount);
            Assert.IsNotNull(settings.Commit);
        }

        [Test]
        public void BackupAndRestore()
        {
            var store = new NFigMemoryStore<InMemorySettings, Tier, DataCenter>(Tier.Local, DataCenter.West);

            // test SET snapshot
            store.SetOverride(APP_NAME, "TopInteger", "7", DataCenter.Any, USER_A);
            store.SetOverride(APP_NAME, "Nested.Integer", "3", DataCenter.West, USER_A);

            var settings = store.GetAppSettings(APP_NAME);
            Assert.AreEqual(7, settings.TopInteger);
            Assert.AreEqual(3, settings.Nested.Integer);
            Assert.AreEqual("Seventeen", settings.Nested.String);

            var snapshot1 = store.GetAppSnapshot(APP_NAME);
            Assert.AreEqual(APP_NAME, snapshot1.ApplicationName);
            Assert.AreEqual(APP_NAME, snapshot1.LastEvent.ApplicationName);
            Assert.AreEqual(settings.Commit, snapshot1.Commit);
            Assert.AreEqual(settings.Commit, snapshot1.LastEvent.Commit);
            Assert.AreEqual(2, snapshot1.Overrides.Count);
            Assert.AreEqual(NFigLogEventType.SetOverride, snapshot1.LastEvent.Type);
            Assert.AreEqual(USER_A, snapshot1.LastEvent.User);
            Assert.AreEqual("Nested.Integer", snapshot1.LastEvent.SettingName);
            Assert.AreEqual("3", snapshot1.LastEvent.SettingValue);
            Assert.IsNull(snapshot1.LastEvent.RestoredCommit);
            Assert.AreEqual(DataCenter.West, snapshot1.LastEvent.DataCenter);

            // test CLEAR snapshot
            store.ClearOverride(APP_NAME, "TopInteger", DataCenter.Any, USER_B);

            settings = store.GetAppSettings(APP_NAME);
            Assert.AreEqual(23, settings.TopInteger);
            Assert.AreEqual(3, settings.Nested.Integer);

            var snapshot2 = store.GetAppSnapshot(APP_NAME);
            Assert.AreEqual(APP_NAME, snapshot2.ApplicationName);
            Assert.AreEqual(APP_NAME, snapshot2.LastEvent.ApplicationName);
            Assert.AreEqual(settings.Commit, snapshot2.Commit);
            Assert.AreEqual(settings.Commit, snapshot2.LastEvent.Commit);
            Assert.AreEqual(1, snapshot2.Overrides.Count);
            Assert.AreEqual(NFigLogEventType.ClearOverride, snapshot2.LastEvent.Type);
            Assert.AreEqual(USER_B, snapshot2.LastEvent.User);
            Assert.AreEqual("TopInteger", snapshot2.LastEvent.SettingName);
            Assert.IsNull(snapshot2.LastEvent.SettingValue);
            Assert.IsNull(snapshot2.LastEvent.RestoredCommit);
            Assert.AreEqual(DataCenter.Any, snapshot2.LastEvent.DataCenter);

            // test RESTORE
            store.SetOverride(APP_NAME, "Nested.String", "Seventy", DataCenter.Any, USER_A);
            settings = store.GetAppSettings(APP_NAME);
            Assert.AreEqual("Seventy", settings.Nested.String);

            var snapshot3 = store.RestoreSnapshot(snapshot1, USER_C);
            settings = store.GetAppSettings(APP_NAME);

            Assert.AreEqual(7, settings.TopInteger);
            Assert.AreEqual(3, settings.Nested.Integer);
            Assert.AreEqual("Seventeen", settings.Nested.String);

            Assert.AreEqual(APP_NAME, snapshot3.ApplicationName);
            Assert.AreEqual(APP_NAME, snapshot3.LastEvent.ApplicationName);
            Assert.AreEqual(settings.Commit, snapshot3.Commit);
            Assert.AreEqual(settings.Commit, snapshot3.LastEvent.Commit);
            Assert.AreEqual(2, snapshot3.Overrides.Count);
            Assert.AreEqual(NFigLogEventType.RestoreSnapshot, snapshot3.LastEvent.Type);
            Assert.AreEqual(USER_C, snapshot3.LastEvent.User);
            Assert.AreEqual(snapshot1.Commit, snapshot3.LastEvent.RestoredCommit);
            Assert.IsNull(snapshot3.LastEvent.SettingName);
            Assert.IsNull(snapshot3.LastEvent.SettingValue);
            Assert.AreEqual(DataCenter.Any, snapshot2.LastEvent.DataCenter);
        }

        [Test]
        public void AnyTierOrDataCenterStoreThrows()
        {
            TestDelegate anyTier = () => { new NFigMemoryStore<InMemorySettings, Tier, DataCenter>(Tier.Any, DataCenter.East); };
            Assert.Throws<ArgumentOutOfRangeException>(anyTier, "NFigStore with Tier.Any should have thrown an exception.");

            TestDelegate anyDc = () => { new NFigMemoryStore<InMemorySettings, Tier, DataCenter>(Tier.Local, DataCenter.Any); };
            Assert.Throws<ArgumentOutOfRangeException>(anyDc, "NFigStore with DataCenter.Any should have thrown an exception.");
        }

        [Test]
        public async Task Logging()
        {
            var logger = new MemoryLogger<Tier, DataCenter>((ex, snapshot) =>
            {
                throw ex;
            });

            var store = new NFigMemoryStore<InMemorySettings, Tier, DataCenter>(Tier.Local, DataCenter.Local, logger);

            const int iterations = 6;
            var totalEvents = 0;
            const string appB = "APP_B";
            for (var i = 0; i < iterations; i++)
            {
                store.SetOverride(APP_NAME, "Nested.Integer", i.ToString(), DataCenter.Any, USER_A);
                totalEvents++;
                store.SetOverride(appB, "Nested.String", "value " + i, DataCenter.Any, USER_B);
                totalEvents++;
            }

            await Task.Delay(10);
            List<NFigLogEvent<DataCenter>> logs;

            // no filter
            logs = (await logger.GetLogsAsync()).ToList();
            Assert.AreEqual(totalEvents, logs.Count);

            // get snapshot by commit
            foreach (var l in logs)
            {
                var snapshot = await logger.GetSnapshotAsync(l.ApplicationName, l.Commit);

                Assert.AreEqual(l.ApplicationName, snapshot.ApplicationName);
                Assert.AreEqual(l.Commit, snapshot.Commit);
                Assert.AreEqual(l.DataCenter, snapshot.LastEvent.DataCenter);
                Assert.AreEqual(l.SettingName, snapshot.LastEvent.SettingName);
                Assert.AreEqual(l.SettingValue, snapshot.LastEvent.SettingValue);
                Assert.AreEqual(l.RestoredCommit, snapshot.LastEvent.RestoredCommit);
                Assert.AreEqual(l.Timestamp, snapshot.LastEvent.Timestamp);
                Assert.AreEqual(l.Type, snapshot.LastEvent.Type);
                Assert.AreEqual(l.User, snapshot.LastEvent.User);
            }

            // by app name
            logs = (await logger.GetLogsAsync(appName: APP_NAME)).ToList();
            Assert.AreEqual(iterations, logs.Count);
            Assert.IsTrue(logs.All(l => l.ApplicationName == APP_NAME));

            logs = (await logger.GetLogsAsync(appName: appB)).ToList();
            Assert.AreEqual(iterations, logs.Count);
            Assert.IsTrue(logs.All(l => l.ApplicationName == appB));

            // by setting
            logs = (await logger.GetLogsAsync(settingName: "Nested.Integer")).ToList();
            Assert.AreEqual(iterations, logs.Count);
            Assert.IsTrue(logs.All(l => l.SettingName == "Nested.Integer"));

            logs = (await logger.GetLogsAsync(settingName: "Nested.String")).ToList();
            Assert.AreEqual(iterations, logs.Count);
            Assert.IsTrue(logs.All(l => l.SettingName == "Nested.String"));

            // by date
            logs = (await logger.GetLogsAsync(minDate: DateTime.MinValue)).ToList();
            Assert.AreEqual(totalEvents, logs.Count);
            logs = (await logger.GetLogsAsync(minDate: DateTime.MaxValue)).ToList();
            Assert.AreEqual(0, logs.Count);
            logs = (await logger.GetLogsAsync(maxDate: DateTime.MaxValue)).ToList();
            Assert.AreEqual(totalEvents, logs.Count);
            logs = (await logger.GetLogsAsync(maxDate: DateTime.MinValue)).ToList();
            Assert.AreEqual(0, logs.Count);

            // by user
            logs = (await logger.GetLogsAsync(user: USER_A)).ToList();
            Assert.AreEqual(iterations, logs.Count);
            Assert.IsTrue(logs.All(l => l.User == USER_A));

            logs = (await logger.GetLogsAsync(user: USER_B)).ToList();
            Assert.AreEqual(iterations, logs.Count);
            Assert.IsTrue(logs.All(l => l.User == USER_B));

            // limit
            logs = (await logger.GetLogsAsync(limit: 3)).ToList();
            Assert.AreEqual(3, logs.Count);

            // skip
            logs = (await logger.GetLogsAsync(skip: 3)).ToList();
            Assert.AreEqual(totalEvents - 3, logs.Count);
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
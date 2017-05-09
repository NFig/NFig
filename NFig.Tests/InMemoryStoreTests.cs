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
        const string USER_A = "Andrew";
        const string USER_B = "Bret";
        const string USER_C = "Charlie";

        [Test]
        public void Defaults()
        {
            var store = Utils.CreateStore<InMemorySettings>(dataCenter: DataCenter.East);
            var settings = store.GetSettingsForGlobalApp();
            Assert.AreEqual(23, settings.TopInteger);
            Assert.AreEqual("Twenty-Three", settings.TopString);
            Assert.AreEqual(17, settings.Nested.Integer);
            Assert.AreEqual("Seventeen", settings.Nested.String);

            store = Utils.CreateStore<InMemorySettings>(tier: Tier.Prod, dataCenter: DataCenter.East);
            settings = store.GetSettingsForGlobalApp();
            Assert.AreEqual(23, settings.TopInteger);
            Assert.AreEqual("Twenty-Three", settings.TopString);
            Assert.AreEqual(7, settings.Nested.Integer);
            Assert.AreEqual("Seven", settings.Nested.String);
        }

        [Test]
        public void Overrides()
        {
            var store = Utils.CreateStore<InMemorySettings>(dataCenter: DataCenter.East);

            store.SetOverride("TopInteger", "3", DataCenter.Any, USER_A);
            store.SetOverride("Nested.Integer", "7", DataCenter.East, USER_A);
            store.SetOverride("Nested.String", "Something", DataCenter.West, USER_A);

            var settings = store.GetSettingsForGlobalApp();
            Assert.AreEqual(3, settings.TopInteger);
            Assert.AreEqual("Twenty-Three", settings.TopString);
            Assert.AreEqual(7, settings.Nested.Integer);
            Assert.AreEqual("Seventeen", settings.Nested.String);
        }

        [Test]
        public void ClearNonExistentOverride()
        {
            var store = Utils.CreateStore<InMemorySettings>();

            var snapshot = store.ClearOverride("TopInteger", DataCenter.Any, USER_A);
            Assert.IsNull(snapshot);
        }

        [Test]
        public void AtomicChanges()
        {
            var store = Utils.CreateStore<InMemorySettings>();

            var snapshot1 = store.SetOverride("TopInteger", "1", DataCenter.Any, USER_A, commit: store.InitialCommit);
            Assert.IsNotNull(snapshot1);

            var snapshot2 = store.SetOverride("TopInteger", "2", DataCenter.Any, USER_A, commit: store.InitialCommit);
            Assert.IsNull(snapshot2);

            var settings = store.GetSettingsForGlobalApp();
            Assert.AreEqual(1, settings.TopInteger);

            var snapshot3 = store.ClearOverride("TopInteger", DataCenter.Any, USER_A, commit: store.InitialCommit);
            Assert.IsNull(snapshot3);

            settings = store.GetSettingsForGlobalApp();
            Assert.AreEqual(1, settings.TopInteger);

            var snapshot4 = store.ClearOverride("TopInteger", DataCenter.Any, USER_A, commit: snapshot1.Commit);
            Assert.IsNotNull(snapshot4);

            settings = store.GetSettingsForGlobalApp();
            Assert.AreEqual(23, settings.TopInteger);
        }

        [Test]
        public void SubscribeToUpdates()
        {
            var store = Utils.CreateStore<InMemorySettings>(dataCenter: DataCenter.West);

            InMemorySettings settings = null;
            var callbackCount = 0;

            store.SubscribeToSettingsForGlobalApp((ex, settingsObj, storeObj) =>
            {
                if (ex != null)
                    throw ex;

                Assert.AreSame(store, storeObj);
                settings = settingsObj;
                callbackCount++;
            });

            Assert.AreEqual(1, callbackCount);
            Assert.IsNotNull(settings);
            Assert.AreEqual(NFigStore.INITIAL_COMMIT, settings.Commit);

            store.SetOverride("Nested.Integer", "32", DataCenter.Any, USER_A);

            Assert.AreEqual(2, callbackCount);
            Assert.IsNotNull(settings.Commit);
        }

        [Test]
        public void BackupAndRestore()
        {
            var store = Utils.CreateStore<InMemorySettings>(dataCenter: DataCenter.West);

            // test SET snapshot
            store.SetOverride("TopInteger", "7", DataCenter.Any, USER_A);
            store.SetOverride("Nested.Integer", "3", DataCenter.West, USER_A);

            var settings = store.GetSettingsForGlobalApp();
            Assert.AreEqual(7, settings.TopInteger);
            Assert.AreEqual(3, settings.Nested.Integer);
            Assert.AreEqual("Seventeen", settings.Nested.String);

            var snapshot1 = store.GetSnapshot();
            Assert.AreEqual(Utils.GLOBAL_APP_1, snapshot1.GlobalAppName);
            Assert.AreEqual(Utils.GLOBAL_APP_1, snapshot1.LastEvent.GlobalAppName);
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
            store.ClearOverride("TopInteger", DataCenter.Any, USER_B);

            settings = store.GetSettingsForGlobalApp();
            Assert.AreEqual(23, settings.TopInteger);
            Assert.AreEqual(3, settings.Nested.Integer);

            var snapshot2 = store.GetSnapshot();
            Assert.AreEqual(Utils.GLOBAL_APP_1, snapshot2.GlobalAppName);
            Assert.AreEqual(Utils.GLOBAL_APP_1, snapshot2.LastEvent.GlobalAppName);
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
            store.SetOverride("Nested.String", "Seventy", DataCenter.Any, USER_A);
            settings = store.GetSettingsForGlobalApp();
            Assert.AreEqual("Seventy", settings.Nested.String);

            var snapshot3 = store.RestoreSnapshot(snapshot1, USER_C);
            settings = store.GetSettingsForGlobalApp();

            Assert.AreEqual(7, settings.TopInteger);
            Assert.AreEqual(3, settings.Nested.Integer);
            Assert.AreEqual("Seventeen", settings.Nested.String);

            Assert.AreEqual(Utils.GLOBAL_APP_1, snapshot3.GlobalAppName);
            Assert.AreEqual(Utils.GLOBAL_APP_1, snapshot3.LastEvent.GlobalAppName);
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
            TestDelegate anyTier = () => { Utils.CreateStore<InMemorySettings>(tier: Tier.Any); };
            Assert.Throws<ArgumentOutOfRangeException>(anyTier, "NFigStore with Tier.Any should have thrown an exception.");

            TestDelegate anyDc = () => { Utils.CreateStore<InMemorySettings>(dataCenter: DataCenter.Any); };
            Assert.Throws<ArgumentOutOfRangeException>(anyDc, "NFigStore with DataCenter.Any should have thrown an exception.");
        }

        [Test]
        public async Task Logging()
        {
            var logger = new NFigMemoryLogger<SubApp, Tier, DataCenter>((ex, snapshot) =>
            {
                throw ex;
            });

            var store = Utils.CreateStore<InMemorySettings>(logger: logger);

            // todo - make this use subapps

            const int iterations = 6;
            var totalEvents = 0;
            const string appB = Utils.GLOBAL_APP_1;
            for (var i = 0; i < iterations; i++)
            {
                store.SetOverride("Nested.Integer", i.ToString(), DataCenter.Any, USER_A); // APP_NAME
                totalEvents++;
                store.SetOverride("Nested.String", "value " + i, DataCenter.Any, USER_B); // appB
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
                var snapshot = await logger.GetSnapshotAsync(l.GlobalAppName, l.Commit);

                Assert.AreEqual(l.GlobalAppName, snapshot.GlobalAppName);
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
            logs = (await logger.GetLogsAsync(globalAppName: Utils.GLOBAL_APP_1)).ToList();
            Assert.AreEqual(iterations * 2, logs.Count); // todo - switch back to just iterations (not x2) when using sub apps
            Assert.IsTrue(logs.All(l => l.GlobalAppName == Utils.GLOBAL_APP_1));

            logs = (await logger.GetLogsAsync(globalAppName: appB)).ToList();
            Assert.AreEqual(iterations * 2, logs.Count); // todo - switch back to just iterations (not x2) when using sub apps
            Assert.IsTrue(logs.All(l => l.GlobalAppName == appB));

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

        class InMemorySettings : SettingsBase
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
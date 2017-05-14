using System;
using System.Collections.Generic;
using NFig.Encryption;
using NFig.InMemory;
using NFig.Logging;

namespace NFig.Tests
{
    static class Utils
    {
        public const string GLOBAL_APP_1 = "GLOBAL_APP_1";
        public const string GLOBAL_APP_2 = "GLOBAL_APP_2";

        public static SettingsFactory<TSettings, SubApp, Tier, DataCenter> CreateFactory<TSettings>(
            string globalAppName = GLOBAL_APP_1,
            Tier tier = Tier.Local,
            DataCenter dataCenter = DataCenter.Local,
            ISettingEncryptor encryptor = null,
            Dictionary<Type, ISettingConverter> additionalConverters = null)
            where TSettings : class, INFigSettings<SubApp, Tier, DataCenter>, new()
        {
            return new SettingsFactory<TSettings, SubApp, Tier, DataCenter>(globalAppName, tier, dataCenter, encryptor, additionalConverters);
        }

        public static NFigStoreOld<TSettings, SubApp, Tier, DataCenter> CreateStore<TSettings>(
            string globalAppName = GLOBAL_APP_1,
            Tier tier = Tier.Local,
            DataCenter dataCenter = DataCenter.Local,
            SettingsLogger<SubApp, Tier, DataCenter> logger = null,
            ISettingEncryptor encryptor = null,
            Dictionary<Type, ISettingConverter> additionalConverters = null)
            where TSettings : class, INFigSettings<SubApp, Tier, DataCenter>, new()
        {
            return new NFigMemoryStore<TSettings, SubApp, Tier, DataCenter>(globalAppName, tier, dataCenter, logger, encryptor, additionalConverters);
        }

        public static OverridesSnapshot<SubApp, Tier, DataCenter> CreateSnapshot(
            string globalAppName = GLOBAL_APP_1,
            string commit = NFigStoreOld.INITIAL_COMMIT,
            List<OverrideValue<SubApp, Tier, DataCenter>> overrides = null,
            NFigLogEvent<DataCenter> lastEvent = null)
        {
            return new OverridesSnapshot<SubApp, Tier, DataCenter>(globalAppName, commit, overrides, lastEvent);
        }

        public static TSettings GetSettings<TSettings>(
            this SettingsFactory<TSettings, SubApp, Tier, DataCenter> factory,
            OverridesSnapshot<SubApp, Tier, DataCenter> snapshot = null)
            where TSettings : class, INFigSettings<SubApp, Tier, DataCenter>, new()
        {
            if (snapshot == null)
                snapshot = CreateSnapshot(factory.GlobalAppName);

            TSettings settings;
            var ex = factory.TryGetSettingsForGlobalApp(out settings, snapshot);
            if (ex != null)
                throw ex;

            return settings;
        }
    }
}
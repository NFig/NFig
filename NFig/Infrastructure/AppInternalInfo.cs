using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NFig.Converters;
using NFig.Encryption;
using NFig.Metadata;

namespace NFig.Infrastructure
{
    class AppInternalInfo<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        internal string AppName { get; }
        [CanBeNull]
        internal Type SettingsType { get; set; }
        [CanBeNull]
        internal ISettingEncryptor Encryptor { get; set; } // todo: eventually make this private
        internal IAppClient AppClient { get; set; }
        internal NFigAdminClient<TTier, TDataCenter> AdminClient { get; set; }

        // Generated internally by the SettingsFactory, not read from the store.
        [CanBeNull]
        internal BySetting<SettingMetadata> GeneratedSettingsMetadata { get; set; }
        [CanBeNull]
        internal Dictionary<string, ISettingConverter> CustomConverters { get; set; }

        internal AppMetadata AppMetadata { get; set; }
        internal Defaults<TTier, TDataCenter> RootDefaults { get; set; }
        internal Dictionary<int, Defaults<TTier, TDataCenter>> SubAppDefaults { get; set; }
        internal OverridesSnapshot<TTier, TDataCenter> Snapshot { get; set; }

        internal bool CanEncrypt => Encryptor != null;
        internal bool CanDecrypt => Encryptor?.CanDecrypt ?? false;

        internal AppInternalInfo(string appName, Type settingsType)
        {
            AppName = appName;
            SettingsType = settingsType;
        }

        internal string Encrypt(string plainText)
        {
            var encryptor = Encryptor;

            if (encryptor == null)
                throw new NFigException($"Cannot encrypt value. App \"{AppName}\" does not have an encryptor set.");

            if (plainText == null)
                return null;

            return encryptor.Encrypt(plainText);
        }

        internal string Decrypt(string encrypted)
        {
            var encryptor = Encryptor;

            if (encryptor == null)
                throw new NFigException($"Cannot decrypt value. App \"{AppName}\" does not have an encryptor set.");

            if (!encryptor.CanDecrypt)
                throw new NFigException($"Cannot decrypt value. App \"{AppName}\" has an encrypt-only encryptor.");

            if (encrypted == null)
                return null;

            return encryptor.Decrypt(encrypted);
        }
    }
}
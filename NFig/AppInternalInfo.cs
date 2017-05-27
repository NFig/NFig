using System;
using JetBrains.Annotations;
using NFig.Encryption;

namespace NFig
{
    class AppInternalInfo
    {
        internal string AppName { get; }
        [CanBeNull]
        internal Type SettingsType { get; set; }
        [CanBeNull]
        internal ISettingEncryptor Encryptor { get; set; } // todo: eventually make this private
        internal object AppClient { get; set; }
        internal object AdminClient { get; set; }

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
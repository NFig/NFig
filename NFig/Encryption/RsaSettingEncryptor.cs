using System;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;

namespace NFig.Encryption
{
    /// <summary>
    /// A Setting encryptor which allows the use of an RSA (asymmetric) encryption algorithm.
    /// </summary>
    public class RsaSettingEncryptor : ISettingEncryptor
    {
        readonly RSA _rsa;
        readonly RSAEncryptionPadding _padding;

        /// <summary>
        /// Uses a pre-initialized RSA object to provide encryption and decryption.
        /// </summary>
        /// <param name="rsa">A pre-initialized RSA provider. Note that this object will be retained for the lifetime of the RsaSettingEncryptor.</param>
        /// <param name="padding">The encryption/decryption padding to use.</param>
        public RsaSettingEncryptor([NotNull] RSA rsa, [NotNull] RSAEncryptionPadding padding)
        {
            if (rsa == null)
                throw new ArgumentNullException(nameof(rsa));

            if (padding == null)
                throw new ArgumentNullException(nameof(padding));

            _rsa = rsa;
            _padding = padding;
        }

        /// <summary>
        /// For internal NFig use only. You should use NFigStore.Encrypt().
        /// </summary>
        public string Encrypt([NotNull] string value)
        {
            var data = Encoding.UTF8.GetBytes(value);
            var encrypted = _rsa.Encrypt(data, _padding);
            return Convert.ToBase64String(encrypted);
        }

        /// <summary>
        /// For internal NFig use only. You should use NFigStore.Decrypt().
        /// </summary>
        /// <param name="encryptedValue"></param>
        /// <returns></returns>
        public string Decrypt([NotNull] string encryptedValue)
        {
            var encrypted = Convert.FromBase64String(encryptedValue);
            var decrypted = _rsa.Decrypt(encrypted, _padding);
            return Encoding.UTF8.GetString(decrypted);
        }
    }
}
using System;
using System.Security.Cryptography;
using System.Text;

namespace NFig.Encryption
{
    public class RsaEncryptor : ISettingEncryptor
    {
        private readonly RSACryptoServiceProvider _rsa;

        /// <summary>
        /// Uses a pre-initialized RSACryptoServiceProvider object to provide encryption and decryption.
        /// </summary>
        public RsaEncryptor(RSACryptoServiceProvider rsa)
        {
            _rsa = rsa;
        }

        /// <summary>
        /// Initializes a RSACryptoServiceProvider with the provided XML key.
        /// </summary>
        /// <param name="key">XML-encoded key. If using a public key, calls to Decrypt will fail. You must provide the private key for use in an NFigStore.</param>
        public RsaEncryptor(string key)
        {
            _rsa = new RSACryptoServiceProvider();
            _rsa.FromXmlString(key);
        }

        public string Encrypt(string value)
        {
            if (value == null)
                return null;

            var data = Encoding.UTF8.GetBytes(value);
            var encrypted = _rsa.Encrypt(data, true);
            return Convert.ToBase64String(encrypted);
        }

        public string Decrypt(string encryptedValue)
        {
            if (encryptedValue == null)
                return null;

            var encrypted = Convert.FromBase64String(encryptedValue);
            var decrypted = _rsa.Decrypt(encrypted, true);
            return Encoding.UTF8.GetString(decrypted);
        }

        /// <summary>
        /// Helper method to generate an RSA public/private key pair.
        /// </summary>
        /// <param name="keySize">Size of the key in bits.</param>
        /// <param name="privateKey">XML-encoded private key which can be used for both encryption and decryption.</param>
        /// <param name="publicKey">XML-encoded public key which can only be used for encryption.</param>
        public static void CreateKeyPair(int keySize, out string privateKey, out string publicKey)
        {
            using (var rsa = new RSACryptoServiceProvider(keySize))
            {
                privateKey = rsa.ToXmlString(true);
                publicKey = rsa.ToXmlString(false);
            }
        }
    }
}
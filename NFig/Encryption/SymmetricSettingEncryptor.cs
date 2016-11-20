using System;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;

namespace NFig.Encryption
{
    /// <summary>
    /// A Setting encryptor which uses a symmetric encryption algorithm (such as AES).
    /// </summary>
    public class SymmetricSettingEncryptor : ISettingEncryptor
    {
        readonly SymmetricAlgorithm _algo;

        /// <summary>
        /// Creates a Setting encryptor based on a symmetric encryption algorithm.
        /// </summary>
        /// <param name="algo">
        /// The pre-initialized symmetric encryption algorithm. Make sure the Key property has been properly set.
        /// Note: this object will be retained for the lifetime of the SymmetricSettingEncryptor.
        /// </param>
        public SymmetricSettingEncryptor([NotNull] SymmetricAlgorithm algo)
        {
            if (algo == null)
                throw new ArgumentNullException(nameof(algo));

            _algo = algo;
        }

        /// <summary>
        /// Takes an unencrypted string and returns a base64-encoded encrypted string. Do not pass null values to this method.
        /// NFig does not support encrypting null values. You should use NFigStore.Encrypt instead of this method, when possible.
        /// </summary>
        public string Encrypt([NotNull] string value)
        {
            var data = Encoding.UTF8.GetBytes(value);
            var encrypted = EncryptTransform(data);
            return Convert.ToBase64String(encrypted);
        }

        /// <summary>
        /// Takes a base64-encoded encrypted string and returns the unencrypted original string. Do not pass null values to this method.
        /// NFig does not support encrypting null values. You should use NFigStore.Decrypt instead of this method, when possible.
        /// </summary>
        public string Decrypt([NotNull] string encryptedValue)
        {
            var encrypted = Convert.FromBase64String(encryptedValue);
            var decrypted = DecryptTransform(encrypted);
            return Encoding.UTF8.GetString(decrypted);
        }
        byte[] EncryptTransform(byte[] input)
        {
            var algo = _algo;

            algo.GenerateIV();
            var iv = algo.IV;

            var encryptor = algo.CreateEncryptor(algo.Key, iv);

            var inputBlockSize = encryptor.InputBlockSize;
            var inputBlockCount = input.Length / inputBlockSize;
            var inputBlockCountRem = input.Length % inputBlockSize;

            var outputBlockCount = inputBlockCountRem == 0 ? inputBlockCount : inputBlockCount + 1;
            var outputSize = outputBlockCount * encryptor.OutputBlockSize;
            if (outputSize == 0)
                outputSize = 1; // even an empty string will generate one output block

            var output = new byte[iv.Length + outputSize];

            // put the IV at the beginning of the output
            Array.Copy(iv, output, iv.Length);

            var inputFullBlockSize = inputBlockSize * inputBlockCount;
            var outputOffset = iv.Length;

            // Encrypt all of the complete blocks
            if (inputBlockCount > 0)
            {
                outputOffset += encryptor.TransformBlock(input, 0, inputFullBlockSize, output, outputOffset);
            }

            // Encrypt the final partial block, if necessary
            if (inputBlockCount == 0 || inputBlockCountRem != 0)
            {
                var final = encryptor.TransformFinalBlock(input, inputFullBlockSize, inputBlockCountRem);
                if (outputOffset + final.Length != outputSize)
                {
                    // bummer, now we have to reallocate. This really shouldn't ever happen, but just in case.
                    var newOutput = new byte[outputOffset + final.Length];
                    Array.Copy(output, newOutput, outputOffset);
                    output = newOutput;
                }

                Array.Copy(final, 0, output, outputOffset, final.Length);
            }

            return output;
        }

        byte[] DecryptTransform(byte[] input)
        {
            var algo = _algo;

            var ivSize = algo.BlockSize / 8;
            var iv = new byte[ivSize];
            Array.Copy(input, iv, ivSize);

            var decryptor = algo.CreateDecryptor(algo.Key, iv);
            return decryptor.TransformFinalBlock(input, ivSize, input.Length - ivSize);
        }
    }
}
namespace NFig.Encryption
{
    /// <summary>
    /// The PassThroughEnctyptor doesn't actually perform any encryption or decryption. It may be useful for testing environments or local tier,
    /// but does not provide any added security at all.
    /// </summary>
    public class PassThroughSettingEncryptor : ISettingEncryptor
    {
        /// <summary>
        /// True if the encryptor can decrypt. This may be false for asymmetric encryption methods where only the public key is available.
        /// </summary>
        public bool CanDecrypt => true;

        /// <summary>
        /// Simply returns the string which is passed to it (no encryption).
        /// </summary>
        public string Encrypt(string value)
        {
            return value;
        }

        /// <summary>
        /// Simply returns the string which is passed to it (no encryption).
        /// </summary>
        public string Decrypt(string encryptedValue)
        {
            return encryptedValue;
        }
    }
}
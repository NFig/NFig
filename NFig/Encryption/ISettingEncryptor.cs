namespace NFig.Encryption
{
    /// <summary>
    /// Interface for encrypting and decrypting the string-representation of settings.
    /// </summary>
    public interface ISettingEncryptor
    {
        /// <summary>
        /// True if the encryptor can decrypt. This may be false for asymmetric encryption methods where only the public key is available.
        /// </summary>
        bool CanDecrypt { get; }
        /// <summary>
        /// Takes an unencrypted string and returns an encrypted version of it.
        /// </summary>
        string Encrypt(string value);
        /// <summary>
        /// Decrypts an encrypted string and returns the original value.
        /// </summary>
        string Decrypt(string encryptedValue);
    }
}
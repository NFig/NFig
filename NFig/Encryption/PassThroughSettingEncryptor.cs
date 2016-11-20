namespace NFig.Encryption
{
    /// <summary>
    /// The PassThroughEnctyptor doesn't actually perform any encryption or decryption. It may be useful for testing environments or local tier,
    /// but does not provide any added security at all.
    /// </summary>
    public class PassThroughSettingEncryptor : ISettingEncryptor
    {
        public string Encrypt(string value)
        {
            return value;
        }

        public string Decrypt(string encryptedValue)
        {
            return encryptedValue;
        }
    }
}
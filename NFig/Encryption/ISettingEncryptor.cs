namespace NFig.Encryption
{
    public interface ISettingEncryptor
    {
        string Encrypt(string value);
        string Decrypt(string encryptedValue);
    }
}
namespace NFig.Encryption
{
    public interface ISettingEncryptor
    {
        string Encrypt(string value);
        string Unencrypt(string encryptedValue);
    }
}
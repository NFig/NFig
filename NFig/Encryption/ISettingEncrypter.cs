namespace NFig.Encryption
{
    public interface ISettingEncrypter
    {
        string Encrypt(string value);
        string Unencrypt(string encryptedValue);
    }
}
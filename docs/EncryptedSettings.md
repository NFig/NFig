# Encrypted Settings

## Usage

#### Creating encrypted settings

Use the `[EncryptedSetting]` attribute, instead of `[Setting]`, for any setting that is encrypted.

``` csharp
[EncryptedSetting]
[Tier(Tier.Prod, "BASE-64-ENCODED ENCRYPTED VALUE")]
public string MyEncryptedSetting { get; private set; }
```

Unlike the `[Setting]` attribute, which _requires_ a default value, the `[EncryptedSetting]` attribute does not accept any default value. The "any tier, any data center" default is always `default(T)` where `T` is the setting type (in the above example, `T = typeof(string)`). All other defaults must be specific to a tier; creating an encrypted default where `tier == 0` (aka the "Any" tier) is not allowed.

#### Setting up the encryptor

An [ISettingEncryptor](https://github.com/NFig/NFig/blob/master/NFig/Encryption/ISettingEncryptor.cs) must be provided to the NFigStore at initialization time if encrypted settings are used:

``` csharp
var encryptor = new RsaSettingEncryptor(rsaObject, padding);
var store = new NFigMemoryStore<Settings, Tier, DataCenter>(tier, dc, encryptor: encryptor);
```

See [ISettingEncryptor Implementations](#isettingencryptor-implementations) below for more information.
#### Setting overrides

If a setting is encrypted, all of its defaults and overrides must be encrypted (there's no mixing and matching for a given setting). The NFigStore provides `Encrypt()` and `Decrypt()` methods to help facilitate this.

``` csharp
var encryptedValue = store.Encrypt("plain text value");
store.SetOverride(appName, settingName, encryptedValue, dc, user);
```
#### How do I know if a setting is encrypted?

[SettingInfo](https://github.com/NFig/NFig/blob/master/NFig/SettingInfo.cs) objects (obtained via `store.GetAllSettingInfos(appName)`) have an `IsEncrypted` property. 

If you just need to know about one setting, the store provides a helper method:

``` csharp
var isEncrypted = store.IsEncrypted(settingName);
```
#### When is the value encrypted or unencrypted?

On the actual settings object (as returned by methods like `store.GetSettingsForGlobalApp()`), the values are always *un*encrypted.

``` csharp
var settings = store.GetAppSettings(appName);
settings.MyEncryptedSetting; // this is the unencrypted value
```

The `Value` property on [SettingValue](https://github.com/NFig/NFig/blob/master/NFig/SettingValue.cs) is encrypted (for any encrypted setting). If you want the unencrypted value, you'll have to call `store.Decrypt()`.

``` csharp
var infos = store.GetAllSettingInfos(appName);
var si = infos.First(i => i.Name == "MyEncryptedSetting");
var settingValue = si.Defaults[0];

var encrypted = settingValue.Value; // this is encrypted if si.IsEncrypted is true
var unencrypted = store.Decrypt(encrypted);
```
>  Think of it this way: the settings object is used by the application, which needs the unencrypted value. The `SettingValue` is metadata used by things like an admin panel. In the admin panel, you might not want to show the unencrypted value by default, or perhaps only allow certain users to view it.

## Limitations and Assumptions

In order to make the encrypted settings implementation practical and maintainable, NFig makes certain assumptions on how they will be used, and imposes limitations to prevent ambiguities.
- It is assumed that all data centers within a given tier will use the same encryptor and encryption keys. Not doing so will typically result in validation exceptions upon initialization.
- Encryption is all or nothing for a given setting. You can't have a setting where some defaults or overrides are encrypted and others are not. However, plain-text defaults on the local tier could be emulated using the [PassThroughSettingEncryptor](#passthroughsettingencryptor), but you can't mix and match encryptors on a single tier.
- As described earlier, for encrypted settings, the "any tier, any data center" default is always `default(T)` where `T` is the setting type. All other defaults must be specific to a tier; creating an encrypted default where `tier == 0` (aka the "Any" tier) is not allowed.
- All defaults must be in encrypted string form. Even if the setting type is an integer, you can't use `5` as a default value; it must be encrypted first and included as a string.
- Null is always considered an unencrypted and unencryptable value. It can be used as a default value, but it will never be passed through the Encryptor. ISettingEncryptor methods do not need to handle null.
- Upon initialization, NFig will assert that the provided `ISettingEncryptor` round-trips correctly. In other words, `ORIGINAL` must _exactly_ equal `Decrypt(Encrypt(ORIGINAL))`.

## ISettingEncryptor Implementations

[ISettingEncryptor](https://github.com/NFig/NFig/blob/master/NFig/Encryption/ISettingEncryptor.cs) is a very simple interface:

``` csharp
public interface ISettingEncryptor
{
    string Encrypt(string value);
    string Decrypt(string encryptedValue);
}
```

> It is expected that the output of `Encrypt()` is a base-64 string, but this is not a strict requirement. For usability, you should avoid encodings which produce characters that require escaping in a C# string.

Any organization with specific security needs can easily provide their own implementation. However, NFig provides three built-in implementations.
### PassThroughSettingEncryptor

This doesn't actually perform any encryption or decryption. Both methods return the original unmodified string.

It may be useful if you want to have encrypted settings for production, but want the convenience of plain text values on the local tier. For example:

``` csharp
[EncryptedSetting]
[Tier(Tier.Local, "Plain text value")]
[Tier(Tier.Prod, "soDCiNqRiCeMoJuMRnjmDacPfp5QVROs5N8XPFL426MyFORRVc4avG8Jdu+gqLjZXFQYXcMz21UdYHBNoc/KB9L9kbzbHPk+4JvWbYL1E2AMa9FsKmzS4k06irBPpb3PWhswMo2QjUwiDYeL0uQicD5cX8Z0uytYmZ0GdmFX5X8KjA+HgkEi00YPQObDvcvvsMb9/cpw7hHQjciPAWqSYaSCdXc28rUL75f889/EWt6tPVcOhDNzUXwW5h2cK0BLcP+qElW7hDh8gt3ltqOj/oR3/iNLyZD4Lj4hAehfFXsUVuu6kFEotwsK1SyTFXHaKWfOpvoEx++mn6USyoK4jsBdyKTDcdPAvaaONaMEX56DiVcMBqVwgIvhMRgrwg4NoVmzdBjAqqn1vzu/Grnwf1P8BsvV9FvBDIorFhYYmiah1Z3Nfwp5iSBet2D3/erJDZihEysmMR0BNi0zTKQlHjwelZZjbW8zGHUVvKU0Aj1ne3IigbamnKjHqBcMRF/jdM8NWe/15/tGsrXP884QwmVZKvNLhiC6K4chMhoOuDg59ap8tufAvJlU6yJBb5ZhK5WRvTOYOz4jlImZdi2MKQYBrAouYB02tQK18LYblxJsmBPMKpI1S8wAFJ4tJbd/2mF9HQ4AdcYWED7PKPtYAGWq8ynWSssqyzbVxe4qYVg=")]
public string EncryptedString { get; private set; }
```

On the local tier, you would use `PassThroughEncryptor`, but in production you would use a real encryptor, such as `RsaSettingEncryptor`.

> More usage can be found in [EncryptedSettingsTests.cs](https://github.com/NFig/NFig/blob/master/NFig.Tests/EncryptedSettingsTests.cs)

### RsaSettingEncryptor

If you want to provide developers with a public key that they can use to encrypt the settings, while still keeping the private decryption key a secret, [RSA encrypton](https://en.wikipedia.org/wiki/RSA_%28cryptosystem%29) may be a good choice.

`RsaSettingEncryptor` accepts an [RSA](https://msdn.microsoft.com/en-us/library/system.security.cryptography.rsa(v=vs.110).aspx) object which it uses to encrypt or decrypt settings. RSA is an abstract class, so you'll want to use [RSACryptoServiceProvider](https://msdn.microsoft.com/en-us/library/system.security.cryptography.rsacryptoserviceprovider%28v=vs.110%29.aspx), [RSACng](https://msdn.microsoft.com/en-us/library/system.security.cryptography.rsacng(v=vs.110).aspx), or any other correct and secure RSA implementation.

You must also provide the [RSAEncryptionPadding](https://msdn.microsoft.com/en-us/library/system.security.cryptography.rsaencryptionpadding(v=vs.110).aspx) to use.

Here is an example using `RSACryptoServiceProvider`:

> This is NOT security advice. It's just a usability example. You should consult your organization's security policies and best practices before implementing anything.

```csharp
var rcp = new RSACryptoServiceProvider();

// privateKey generated from (new RSACryptoServiceProvider(KEY_SIZE)).ToXmlString(true)
// Make sure you store it securely.
rcp.FromXmlString(privateKey);

// create setting encryptor
var encryptor = new RsaSettingEncryptor(rcp, RSAEncryptionPadding.OaepSHA1);

// now you can pass encryptor to an NFigStore constructor
var store = new NFigMemoryStore<Settings, Tier, DataCenter>(tier, dc, encryptor: encryptor);
```

The _private_ key must always be used for the encryptor passed to an NFigStore, but you can use the _public_ key to create an `RsaSettingEncryptor` that only supports encryption.
### SymmetricSettingEncryptor

If you don't need a public key, you can use symmetrical encryption (such as [AES](https://en.wikipedia.org/wiki/Advanced_Encryption_Standard)).

The `SymmetricSettingEncryptor` constructor accepts any object which inherits from [SymmetricAlgorithm](https://msdn.microsoft.com/en-us/library/system.security.cryptography.symmetricalgorithm(v=vs.110).aspx). The object must have the `Key` property set, but `IV` does not need to be set (`IV` will change every time `Encrypt()` is called).

Here is an example using [AesCryptoServiceProvider](https://msdn.microsoft.com/en-us/library/system.security.cryptography.aescryptoserviceprovider(v=vs.110).aspx):

> This is NOT security advice. It's just a usability example. You should consult your organization's security policies and best practices before implementing anything.

```csharp
var aes = new AesCryptoServiceProvider();

// A byte array you can generate using aes.GenerateKey().
// Make sure you store it securely.
aes.Key = mySecureKey;

// create setting encryptor
var encryptor = new SymmetricSettingEncryptor(aes);

// now you can pass encryptor to an NFigStore constructor
var store = new NFigMemoryStore<Settings, Tier, DataCenter>(tier, dc, encryptor: encryptor);
```


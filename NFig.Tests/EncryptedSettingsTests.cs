using System;
using System.Collections.Generic;
using NFig.Encryption;
using NFig.InMemory;
using NUnit.Framework;

namespace NFig.Tests
{
    public class EncryptedSettingsTests
    {
        private const string APP_NAME = "TestApp";
        private const string USER_A = "Andrew";
        private const string USER_B = "Bret";
        private const string USER_C = "Charlie";

        private class MultipleAttributesSettings : SettingsBase
        {
            [EncryptedSetting]
            [Setting(null)]
            public string BadSetting { get; private set; }
        }

        [Test]
        public void MultipleAttributes()
        {
            Assert.Throws<NFigException>(() =>
            {
                new NFigMemoryStore<MultipleAttributesSettings, Tier, DataCenter>(Tier.Local, DataCenter.Local, encryptor: new PassThroughEncryptor());
            });
        }

        private class NoTierDefaultsSettings : SettingsBase
        {
            [EncryptedSetting]
            [DataCenter(DataCenter.Local, "value")]
            public string BadSetting { get; private set; }
        }

        [Test]
        public void NoTierDefaults()
        {
            Assert.Throws<NFigException>(() =>
            {
                new NFigMemoryStore<NoTierDefaultsSettings, Tier, DataCenter>(Tier.Local, DataCenter.Local, encryptor: new PassThroughEncryptor());
            });
        }

        [Test]
        public void InvalidEncryptor()
        {
            Assert.Throws<NFigException>(() =>
            {
                new NFigMemoryStore<SettingsBase, Tier, DataCenter>(Tier.Local, DataCenter.Local, encryptor: new InvalidSettingEncryptor());
            });
        }

        [Test]
        public void PassThroughEncryption()
        {
            var passThrough = new PassThroughEncryptor();

            var store = new NFigMemoryStore<Settings, Tier, DataCenter>(Tier.Local, DataCenter.Local, encryptor: passThrough);
            var settings = store.GetAppSettings(APP_NAME);

            Assert.AreEqual("Plain text value", settings.EncryptedString);
            Assert.AreEqual("More plain text", settings.UnencryptedString);
            Assert.AreEqual(3, settings.EncryptedList.Length);
            Assert.AreEqual(1, settings.EncryptedList[0]);
            Assert.AreEqual(2, settings.EncryptedList[1]);
            Assert.AreEqual(3, settings.EncryptedList[2]);
            Assert.AreEqual(0, settings.EncryptedInt);
            Assert.AreEqual(17.3, settings.EncryptedDouble);
        }

        [Test]
        public void RsaEncryption()
        {
            // if you're looking at this test as an example, remember, you shouldn't store your real private key in source code,
            // since that defeats the purpose of encrypted settings in the first place.
            const string privateKey = "<RSAKeyValue><Modulus>wzbOAGJk14Bzfo11NXR+kit6wVjn9BGNO1rElbVk7O3Bs70QQNJs99lLw0KC5ZLIvKUs9oVx8dqBMYgoh2j3Z1olvi0nU2NAmISTyDXYLa6WjY5cdCUBtLHCtouUwgdo94Fc0dqKpGocvyPsyxxPr/DqkuRYbHbpzrBru+0s/ej8tgw7xA4CKQf/A7li5X8dGvYD2pC0qOF1zGkTrHK83S4XJ3erVZgdJ5fTp0Ecy6WvvowNjLc6UjLCDGbQL9vo/Z2s7UhVqUP9VxFADYswrPxhImDVfAe8JYo2yNpE6AIc/ntq5KWWUl50LVqBy3siCbRk+EpmlsITueQ9JYFe/vPO2MLLhir3ILPwL7UVzNBLN3reW8BBgk5y7Kca+u/odDC5y7E+vWV6MMC0zafjkWFFkH9Oo7nRzAn8A+479qIo9UzDH7rN1q7z60hVT97JVimRhKbaIGcRrdNGi+Nfg18UdS+spTu7zW719IVgEg8D3ZAEfITKH4mYBlnTjNQD2Q7BorTtOs+FFxf7U8+KGEHGc0Zyw1MJzPqXtBrRYbh+6tQQVFg8MioqLs2ZHOLKaner/IKpOMyf2231tS52yxYEcHJF7KA5sLcZCgVGHHet3H3dSshsGnVQW28yi9LUI9G8bW8PXxqNBBklA2f5wad8+2zHSFHFlbCp8O4iOF8=</Modulus><Exponent>AQAB</Exponent><P>xaXluoZkfSD10RNNx9NGa8HmBJ9zmwhd1AoB6fUggitGNFJo/voL9RHeFHLz85YDOqWiYdVetajDcmx22ZCfW+EtyOUA9BiHQ8iZ+B/QFDFkC6GFpbBQ4JnIUYCInTvSr11L41SuIpQmexICZbP85wB3aPzImEdJl/xMeFJ0Tbqa3M4W+5/VuFd4LI9fmIEYMQXfV4Hwv8aEZePsvZqwF3Vy1uvYasd80ZpxrKQgDPuRa2EPdktoEHkecq7AvKuQi08JCXoL3Oy7tF886EAU8tBifkKBfSqdxbjzVDYag1WjJMLHyX/FSdaimGIObjm3xqZ6j0CkzF+aC1ybvm9zRQ==</P><Q>/NjzW0WJbMJfPnSYENecca1Z5n0bXBskq7IWi+LDghyIRqZl3cVcKQFp7g6HR3VI1Ls1qUBad9nhFzmCH7RJ8XvriGgWw44wKZWBTScJEkE8RP8e8ismmUqDLZbb+bGL9vjcs+JHEz9bVpfBpTJVzaX5SGhvqTi6eV+h8ng5fiE5mVqRjMBQR3z+fzKg8pM1P9I23CClfuzAQr6lTo0A3+P0DIc5lMip2dtiShQOrMMZVuSumBFVMfD0eXDzKyhgkLeXjOKWi9HWFJGnIKt+O3dJYlazE5dcg38ffzQIEZBqGL0oh3IdbhQqgT6g9X1ZseP+OP7Wbtn7XLeDr/mFUw==</Q><DP>dH7H3R0Bdc9LlCPwoGsjAriSvv7MwQA5bZVIc4GL261t/8DjKgZvrc8OOrdWmqg81wBxqYB+BkymhnbvxmS7yQf8WIDCAx3B/G3scpctqflCoqhdgb9erEN4ErHT0/lCwSIYbLGowbDYzYlb6F4iHnXj6/mysi6ybebDm9fdvULTrHm8iis3aSQFLR6ElfrhK2PutEFeiWqU9wlUrJzUVb9gJNV2Bdn29AQ4JC3Ixl4w5D2dQ3hGqg75p1bhO1NUKwg2p2NMQrc2G6ZW2/2JW2T/6LOCZygLPlM4+NW64tZDBpPX9ihdPJxJ0c9Z+hYDAA94BZ9wgxWGUlQPaDuAkQ==</DP><DQ>8hZWiqD9fyBrSMUhg56srzqmxMQsGffzGGEerTiksELZR6uyBLAeGTS9U6ydYZGt9eB49GAlPnNhzHbHW8umsItRa/0dLodJHceDUXd6e/vx1K3f10XxYvuwtUmnvF5+AC0uQxz3qDoVHceXJLAY7xlmoCk+H+usvuENkbYCdf0hxO6uRPEs7AWFNgwnhZnkdgKze/fV/Lx5KG6Yn6jpmXQMCqK+QvINjgU7CmpB0q/J5yR09ixCQdrOeCfo6v1x152wgLfCJIT4UCFUvzbzWcRQgtM4Ch31+gERCx2qTAbVTnJuB6D/BJJSUATC38jwxB3jHncAoIoc3Rzn/OkO+Q==</DQ><InverseQ>kEsvjO18zQul2vq/1HR8zR+FHGOZ+Rk6/HJWRoTyqexz40rs1cs6oYBZQ7LU5ngmJ0T7hwI1v1KYCRetDIf+3ff3ldR5HKeaHXK9Dp1H7PDMNEwjfDtj+HxzBvxKVlxR+y53aLzUpPKzB4tC+5k0wd3Yr77hl5aAruf1VSrI9WcG7zdW6e1Mq/gZlqmlo3mZSBMp+4oj1fuNIZr5tyh9RdCo+yn7ZThb/IvvbRxVxLdY+erwjv8rOmMBTelmh72QOFBaDuSIw1Hlv0tpynp7XdY2cvlDJTmq0Sz0s4hOs/1JaBj/caThYdlWwzbcD5bFem4pZiHJNvPBHVwCWhmG/w==</InverseQ><D>RcxMJq4HoVfvs6GPdW/0K345U3Ve5hD6fuzq2h5z+hTei83/SDYUuR+sK6IV3FC5zl1+sJwxDAkU5WlqGFRrFTyRvyg05edYes/4aP77jwXcFbv/iZWLwRhH5u4MX723hbtuvSfXJ4c7RQpqyYqTYXSR4ribdxija3//3T8ltZl+9fZ0zho2IoaV4zZ/SlBDT1cENLtFpRaMAGzXmHKj3a9znux2SdHsJrJt/mDVE2ln54j69UO0Khy07is6p8oIydl9MZAJ9M33AJEN5mnMmyVX86tM2Z6IYqHDCdilB5Ft3X2yUBN2pnfYMTkprGAX4QFrkq1Ddgbpd69IP6c8l+nwURoBf2ZRViAwG6ygW4q2pJL1d6u3OrYl/8OnWcQg+e79FElER/ESNgh/oCqDv3FTBrKjgRVu3dBQs3U3+j/F7h9Aagf6jn3sO2ZEXyV092scYPhrjGnVLtGr1HDMZR3bCNcASVIrZxHUYocADe+Y9Oq6ZnVqw1yfm+0fLmOjr1UCaXzRas4BuLJJYuDY/w3qFEQ8Q8oAUJiBPc6hVK4SpFS9XbIHvcptYQdwgCcT6NVKu+zoRcm6hjkKFAW2B5sPaakXBi7FKl974m0S0AB69cndRNZdUUiC3D87DooSnWceI5xHbah0mJaOsV2enJEEq7p5RdaYcyeAzdnNmiU=</D></RSAKeyValue>";
            var rsa = new RsaEncryptor(privateKey);

            var store = new NFigMemoryStore<Settings, Tier, DataCenter>(Tier.Prod, DataCenter.East, encryptor: rsa);
            var settings = store.GetAppSettings(APP_NAME);

            Assert.AreEqual("Encrypted text value", settings.EncryptedString);
            Assert.AreEqual("Also plain text", settings.UnencryptedString);
            Assert.AreEqual(3, settings.EncryptedList.Length);
            Assert.AreEqual(4, settings.EncryptedList[0]);
            Assert.AreEqual(5, settings.EncryptedList[1]);
            Assert.AreEqual(6, settings.EncryptedList[2]);
            Assert.AreEqual(0, settings.EncryptedInt);
            Assert.AreEqual(23.37, settings.EncryptedDouble);

            const string unencrypted = "Override text";
            const string encrypted = "PspZKNcOqMc7SGRuH0Mmva59xjhpezVcfEfMzQmuCvG7pbUmIu+BZ0C1aw4Dur2vL95/7aCv/LZq/bX0ITmzSqwtkIHvE6/L0G9vdh/sSOlgIG+a+8TSkjPUw2KaPNmUQxH4/RP5/3wGQrOdIvcs9HtJd+VB3BFK7f6BkaU6rdrJNcOWn0U6tz+O1KvUsaWBO8wolF6bD6wk/w9Vayyq2f+Fq2g/V4IdgZ2LPOl9a9Y2QtI18eNsTxCPwKiFzqaaYhFQt6qSOHGk1cbOw7MA46yrjCliqos4l9ORX5uYHTOOpN10BoEOSiQgf3/fauT907Y7OmwTUzhJ3wxQjk1z2NP3Hqzsj89XiMHJ5mkzMzxSc3SoNnG5yH3bAehOs9xKUA+tiFdL+smfQICXe+chroSW21EnG7i84iu6l3dbLwNHssuv9mb7j2MtQ0ujUvkjLu3ZKvfpa2ksyTMdEhnT0BkT/u7uhLhLswdxqzCxb1osrOD5F6b0UGo3/Yot/lmxlbDZP2UvFcDL1lR4Po5ua3WojTUbb+uXOI+fSbzoRB1ofUJlLZ4GGlQDTljYghDBSU9Cknlgx6EvujunMW71+6D7GUjt7+TbxTsb0OLZag+Bd7iQ20ycw5N7UCJL+4uLpFqzgCjpXxL8WBa1QLgJpviy1PszHbnpLL18hze6zwI=";

            Assert.IsFalse(store.IsValidStringForSetting("EncryptedString", unencrypted));
            Assert.IsTrue(store.IsValidStringForSetting("EncryptedString", encrypted));
            Assert.IsTrue(store.IsValidStringForSetting("UnencryptedString", unencrypted));
            Assert.IsTrue(store.IsValidStringForSetting("UnencryptedString", encrypted));

            Assert.Throws<InvalidSettingValueException>(() =>
            {
                store.SetOverride(APP_NAME, "EncryptedString", unencrypted, DataCenter.Any, USER_A);
            });

            store.SetOverride(APP_NAME, "EncryptedString", encrypted, DataCenter.Any, USER_A);
            settings = store.GetAppSettings(APP_NAME);
            Assert.AreEqual(unencrypted, settings.EncryptedString);
        }
        
        // Local for PassThrough testing
        // Prod for RSA testing
        private class Settings : SettingsBase
        {
            [EncryptedSetting]
            [Tier(Tier.Local, "Plain text value")]
            // "Encrypted text value"
            [Tier(Tier.Prod, "soDCiNqRiCeMoJuMRnjmDacPfp5QVROs5N8XPFL426MyFORRVc4avG8Jdu+gqLjZXFQYXcMz21UdYHBNoc/KB9L9kbzbHPk+4JvWbYL1E2AMa9FsKmzS4k06irBPpb3PWhswMo2QjUwiDYeL0uQicD5cX8Z0uytYmZ0GdmFX5X8KjA+HgkEi00YPQObDvcvvsMb9/cpw7hHQjciPAWqSYaSCdXc28rUL75f889/EWt6tPVcOhDNzUXwW5h2cK0BLcP+qElW7hDh8gt3ltqOj/oR3/iNLyZD4Lj4hAehfFXsUVuu6kFEotwsK1SyTFXHaKWfOpvoEx++mn6USyoK4jsBdyKTDcdPAvaaONaMEX56DiVcMBqVwgIvhMRgrwg4NoVmzdBjAqqn1vzu/Grnwf1P8BsvV9FvBDIorFhYYmiah1Z3Nfwp5iSBet2D3/erJDZihEysmMR0BNi0zTKQlHjwelZZjbW8zGHUVvKU0Aj1ne3IigbamnKjHqBcMRF/jdM8NWe/15/tGsrXP884QwmVZKvNLhiC6K4chMhoOuDg59ap8tufAvJlU6yJBb5ZhK5WRvTOYOz4jlImZdi2MKQYBrAouYB02tQK18LYblxJsmBPMKpI1S8wAFJ4tJbd/2mF9HQ4AdcYWED7PKPtYAGWq8ynWSssqyzbVxe4qYVg=")]
            public string EncryptedString { get; private set; }

            [Setting(null)]
            [Tier(Tier.Local, "More plain text")]
            [Tier(Tier.Prod, "Also plain text")]
            public string UnencryptedString { get; private set; }

            [EncryptedSetting]
            [Tier(Tier.Local, "1,2,3")]
            // "4,5,6"
            [Tier(Tier.Prod, "VGhO0RM+F1tto+5z5ECgsIab0C2nh0zW7LLEPhKFgb+dgkdEL00FKHIyRmNcA6oRkLMuUR1WyrXTKD4BWO3JWZ+6pFJWcf8FN3Gpciso9sh/wUgKmeZXxtRk87Ve0GWSdyDfYvGISVHo3pwLujBng8RHLvPu3uBmfOs4fPdshsDjU5sKu4nz6ufyONsFd2wUlHcVJXgzVCFA6aY27FHz6mPWLYprvvEAXSKuA3v7/tP7O7t/wvFeOGv+SowgnngNySQlIFusByAchuh6ubR2qOGTh2yKCe9uPRdqJBzUK5g3XTv3DSHFkvF1BXMHWg6PoeRIXMQVzgy7lYq7kR4fh87QSmmH5m6HWJK+S2F20sUnxxkazjWvA7twj+8woPij5kn4MQ57k7x2KQsdbe91xgE+GvMfRXRwE1s3s9PR14nZyIOub11RnmqFaElmK4eli6D360hqalxBbMxvtC8XfOX0uhDW3CgunS4QPRSAfNZPB/vUCvgLqpfg1/MlpZ7AmTlHTIKI0KFu3fOVUfvSPY+9b3vlBBCN8BxfsAMLJyMwxwFvUfGmT/NaZZwSlehQF/TBRfb7qEBz0qg9/CToQg+2vjygNXzHP5rWUw1vOk3OcyDqHnuAdTz65zbAh0LUvQqVH+b7vUoEW2/g6TBNY5HJtLsTRXbjXR1LD5NMw+E=")]
            [SettingConverter(typeof(CustomConverterTests.IntArrayConverter))]
            public int[] EncryptedList { get; private set; }

            [EncryptedSetting]
            public int EncryptedInt { get; private set; }

            [EncryptedSetting]
            [Tier(Tier.Local, "17.3")]
            // 23.37
            [Tier(Tier.Prod, "m2rmrNgFPLIunt6KC5gSMDVHd0LsMpSwr8QDhg1T30p2BI3Ei1nzSGJzqu8XCTg+M2EwKyLQ78lYXooBYpesodJ72GxjuZMmnK2y7geQiSa6reYXQffYRfkbGOLKz1OJ6ERNQMojSLcyAh4rhec2zp6sQasjeiHKSej1JOaGY6p7THO6fiForx+uBgw64SW7YSAN5hNOljamttIvvKCq3cXqKWmDK06zzLjvvt9QQLY9u9zwwih+KxCmW2/B0JqH+DheLwztNeK/yHaqBkdDXCrijHKw2uInI32Er2VZYdlHC/lH8moWftCNnJK+nRTS47Zuh+hA326PziyeSWChOb9x9zFRe5zqbA9m9CG3xmsQaommvFvIZwElmY/rh7Cp+hnS7+pGYTZwhk2IS6Z8W3tokujivHfZ0vBvrikJ/T7xM09tCH59EPrnvm2uDvA/buGnNbNGOtFA1T12Z5qHKOtwFEurkcERijkxjUiOXML+ZnT2PffdCZa42ZyQWY9Fr4vV+17ktNs4Rkmv/WimMPH/w63057LffofMqQFc8Uj2BS4Tuo48L0c+mAJkSDviaRIS9GtMi+gI2Zdth/WtsXQT+mQcPwG3uuVKEIa3l7wjwDIaUSbGGAc+BjBz3A0MwGrGAGLu3DcfBSRQoQdOuDKDejMlaXobPxBC3kWaeTE=")]
            public double EncryptedDouble { get; private set; }
        }

        // an encryptor which doesn't round-trip properly
        private class InvalidSettingEncryptor : ISettingEncryptor
        {
            public string Encrypt(string value)
            {
                return Guid.NewGuid().ToString();
            }

            public string Decrypt(string encryptedValue)
            {
                return Guid.NewGuid().ToString();
            }
        }
    }
}
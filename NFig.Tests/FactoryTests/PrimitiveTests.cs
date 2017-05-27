using NFig.Tests.Common;
using NUnit.Framework;

namespace NFig.Tests.FactoryTests
{
    [TestFixture]
    public class PrimitiveTests
    {
        [Test]
        public void PrimitivesTest()
        {
            var factory = Utils.CreateFactory<PrimitiveSettings>();
            var s = factory.GetSettings();

            Assert.True(s.Bool == true, "Bool");
            Assert.True(s.Byte == 128, "Byte");
            Assert.True(s.Short == -1200, "Short");
            Assert.True(s.UShort == 1200, "UShort");
            Assert.True(s.Int == -85000, "Int");
            Assert.True(s.UInt == 85000, "UInt");
            Assert.True(s.Long == -8000000000, "Long");
            Assert.True(s.ULong == 8000000000, "ULong");
            Assert.True(s.Float == 3.14f, "Float");
            Assert.True(s.Double == 2.737, "Double");
            Assert.True(s.String == "hey", "String");
            Assert.True(s.Char == 'c', "Char");
            Assert.True(s.Decimal == 0.2m, "Decimal");
        }

        class PrimitiveSettings : SettingsBase
        {
            [Setting(true)]
            public bool Bool { get; private set; }

            [Setting(128)]
            public byte Byte { get; private set; }

            [Setting(-1200)]
            public short Short { get; private set; }

            [Setting(1200)]
            public ushort UShort { get; private set; }

            [Setting(-85000)]
            public int Int { get; private set; }

            [Setting(85000)]
            public uint UInt { get; private set; }

            [Setting(-8000000000)]
            public long Long { get; private set; }

            [Setting(8000000000)]
            public long ULong { get; private set; }

            [Setting(3.14)]
            public float Float { get; private set; }

            [Setting(2.737)]
            public double Double { get; private set; }

            [Setting("hey")]
            public string String { get; private set; }

            [Setting('c')]
            public char Char { get; private set; }

            [Setting("0.2")]
            public decimal Decimal { get; private set; }
        }
    }
}
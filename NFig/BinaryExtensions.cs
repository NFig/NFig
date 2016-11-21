using System.IO;

namespace NFig
{
    static class BinaryExtensions
    {
        public static void WriteNullableString(this BinaryWriter w, string str)
        {
            if (str == null)
            {
                w.Write((byte)0);
            }
            else
            {
                w.Write((byte)1);
                w.Write(str);
            }
        }

        public static string ReadNullableString(this BinaryReader r)
        {
            if (r.ReadByte() == 0)
                return null;

            return r.ReadString();
        }
    }
}
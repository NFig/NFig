using System.Collections.Generic;

namespace NFig
{
    internal static class Compare
    {
        public static bool AreEqual<T>(T a, T b)
        {
            return EqualityComparer<T>.Default.Equals(a, b);
        }

        public static bool IsDefault<T>(T a)
        {
            return AreEqual(a, default(T));
        }
    }
}
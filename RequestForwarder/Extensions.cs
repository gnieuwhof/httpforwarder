namespace RequestForwarder
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public static class Extensions
    {
        public static int GetEndIndex(this IList<byte> haystack, string needle, int startIndex = 0)
        {
            if (haystack == null)
                throw new ArgumentNullException(nameof(haystack));
            if (needle == null)
                throw new ArgumentNullException(nameof(needle));

            byte[] needleBytes = Encoding.ASCII.GetBytes(needle);

            return haystack.GetEndIndex(needleBytes, startIndex);
        }

        public static int GetEndIndex(this IList<byte> haystack, byte[] needle, int startIndex = 0)
        {
            if (needle == null)
                throw new ArgumentNullException(nameof(needle));
            if (haystack == null)
                throw new ArgumentNullException(nameof(haystack));

            if (needle.Length <= haystack.Count)
            {
                int needleIndex = 0;
                for (int i = startIndex; i < haystack.Count; ++i)
                {
                    if (needle[needleIndex] == haystack[i])
                    {
                        ++needleIndex;
                    }
                    else
                    {
                        // Reset
                        needleIndex = 0;
                    }

                    if (needleIndex == needle.Length)
                    {
                        // Done
                        return i;
                    }
                }
            }

            return -1;
        }

        public static T[] Take<T>(this T[] arr, int count)
        {
            if (count < arr.Length)
            {
                var temp = new T[count];
                Array.Copy(arr, temp, count);
                return temp;
            }

            return arr;
        }

        public static IList<byte> Replace(this List<byte> bytes, int start, int length, byte[] newBytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (newBytes == null)
                throw new ArgumentNullException(nameof(newBytes));

            int newLength = (bytes.Count - length + newBytes.Length);

            var result = new List<byte>(newLength);
            result.AddRange(bytes.GetRange(0, start));
            result.AddRange(newBytes);
            result.AddRange(bytes.GetRange(start + length, bytes.Count - (start + length)));

            return result;
        }
    }
}

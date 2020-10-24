using System;
using System.Text;

namespace Cobble.Extensions
{
    public static class SpanByteExtensions
    {
        public static int WriteVarInt(this ref Span<byte> buffer, int value)
        {
            var i = 0;
            for (; (value & 128) != 0; i++)
            {
                buffer[i] = (byte)(value & 127 | 128);
                value = (int)((uint)value >> 7);
            }
            buffer[++i] = (byte)value;
            return i;
        }

        public static int WriteString(this ref Span<byte> buffer, string value)
        {
            var varIntLength = buffer.WriteVarInt(value.Length) + 1;
            Encoding.UTF8.GetBytes(value).CopyTo(buffer[varIntLength..]);
            return varIntLength + value.Length;
        }
    }
}

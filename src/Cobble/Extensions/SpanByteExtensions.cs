using System;
using System.Text;

namespace Cobble.Extensions
{
    public static class SpanByteExtensions
    {
        public static int WriteVarInt(this Span<byte> buffer, int value)
        {
            var i = 0;
            for (; (value & 128) != 0; i++)
            {
                buffer[i] = (byte)(value & 127 | 128);
                value = (int)((uint)value >> 7);
            }
            buffer[i] = (byte)value;
            return i + 1;
        }

        public static int WriteString(this Span<byte> buffer, string value)
        {
            var varIntLength = buffer.WriteVarInt(value.Length);
            var bytes = Encoding.UTF8.GetBytes(value);
            bytes.CopyTo(buffer[varIntLength..]);
            return varIntLength + value.Length;
        }
    }
}

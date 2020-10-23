using System;
using System.Buffers;
using System.Linq;
using System.Text;

namespace Cobble.Minecraft.Extensions
{
    public static class SequenceReaderExtensions
    {
        public static int ReadVarInt(this ref SequenceReader<byte> reader)
        {
            int numRead = 0, result = 0;

            while (reader.TryRead(out byte read))
            {
                int value = read & 0b_0111_1111;
                result |= value << (7 * numRead);

                numRead++;
                if (numRead > 5)
                    throw new Exception("VarInt is too big");
                else if ((read & 0b_1000_0000) == 0)
                    break;
            }

            return result;
        }

        public static long ReadVarLong(this ref SequenceReader<byte> reader)
        {
            int numRead = 0;
            long result = 0;

            while (reader.TryRead(out byte read))
            {
                long value = read & 0b_0111_1111;
                result |= value << (7 * numRead);

                numRead++;
                if (numRead > 10)
                    throw new Exception("VarLong is too big");
                else if ((read & 0b_1000_0000) != 0)
                    break;
            }

            return result;
        }

        public static string ReadString(this ref SequenceReader<byte> reader)
        {
            var length = reader.ReadVarInt();
            var slice = reader.UnreadSequence.Slice(0, length);
            reader.Advance(length);
            return Encoding.UTF8.GetString(slice);
        }

        public static ushort ReadUShort(this ref SequenceReader<byte> reader)
        {
            var slice = reader.UnreadSequence.Slice(0, 2).ToArray();
            reader.Advance(2);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(slice);
            }
            return BitConverter.ToUInt16(slice, 0);
        }
    }
}

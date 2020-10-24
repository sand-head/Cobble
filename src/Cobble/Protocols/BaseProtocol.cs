using Bedrock.Framework.Protocols;
using Cobble.Extensions;
using Cobble.Packets;
using System;
using System.Buffers;

namespace Cobble.Protocols
{
    public abstract class BaseProtocol : IMessageReader<Packet>, IMessageWriter<Packet>
    {
        public abstract Packet GetPacket(ref SequenceReader<byte> reader, int length, int packetId);

        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out Packet message)
        {
            var reader = new SequenceReader<byte>(input);
            var length = reader.ReadVarInt();
            var packetId = reader.ReadVarInt();

            message = GetPacket(ref reader, length, packetId);

            consumed = reader.Position;
            examined = consumed;
            return message != null;
        }

        public void WriteMessage(Packet message, IBufferWriter<byte> output)
        {
            var payloadBuffer = message.ToSpan();

            var packetIdBuffer = new Span<byte>(new byte[5]);
            var packetIdLen = packetIdBuffer.WriteVarInt(message.PacketId);

            var buffer = output.GetSpan(5 + packetIdLen + payloadBuffer.Length);
            var lengthLen = buffer.WriteVarInt(packetIdLen + payloadBuffer.Length);
            packetIdBuffer.CopyTo(buffer[lengthLen..]);
            payloadBuffer.CopyTo(buffer[(lengthLen + packetIdLen)..]);

            var totalLen = lengthLen + packetIdLen + payloadBuffer.Length;
            output.Write(buffer[0..totalLen]);
        }
    }
}

using Bedrock.Framework.Protocols;
using Cobble.Extensions;
using Cobble.Packets;
using System;
using System.Buffers;

namespace Cobble
{
    public class MinecraftProtocol : IMessageReader<Packet>, IMessageWriter<Packet>
    {
        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out Packet message)
        {
            var reader = new SequenceReader<byte>(input);
            var length = reader.ReadVarInt();
            var packetId = reader.ReadVarInt();

            message = (length, packetId) switch
            {
                (1, 0) => new Request(),
                (int x, 0) when x > 1 => new Handshake(
                    ProtocolVersion: reader.ReadVarInt(),
                    Address: reader.ReadString(),
                    Port: reader.ReadUShort(),
                    State: reader.ReadVarInt()),
                (_, 1) => new Ping(Payload: reader.ReadLong()),
                // todo: add other packets to parse
                _ => null
            };

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

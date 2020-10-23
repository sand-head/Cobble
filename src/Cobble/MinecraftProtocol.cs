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
                (_, 0) => new Handshake(
                    ProtocolVersion: reader.ReadVarInt(),
                    Address: reader.ReadString(),
                    Port: reader.ReadUShort(),
                    State: reader.ReadVarInt()),
                // todo: add other packets to parse
                _ => null
            };

            consumed = reader.Position;
            examined = consumed;
            return true;
        }

        public void WriteMessage(Packet message, IBufferWriter<byte> output) => message.Write(output);
    }
}

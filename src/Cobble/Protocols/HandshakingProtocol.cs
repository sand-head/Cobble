using Cobble.Extensions;
using Cobble.Packets;
using System.Buffers;

namespace Cobble.Protocols
{
    public class HandshakingProtocol : BaseProtocol
    {
        public override Packet GetPacket(ref SequenceReader<byte> reader, int length, int packetId)
        {
            return (length, packetId) switch
            {
                (int x, 0) when x > 1 => new Handshake(
                    ProtocolVersion: reader.ReadVarInt(),
                    Address: reader.ReadString(),
                    Port: reader.ReadUShort(),
                    NextState: (PacketState)reader.ReadVarInt()),
                _ => null
            };
        }
    }
}

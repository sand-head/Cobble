using Cobble.Extensions;
using Cobble.Packets;
using System.Buffers;

namespace Cobble.Protocols
{
    public class StatusProtocol : BaseProtocol
    {
        public override Packet GetPacket(ref SequenceReader<byte> reader, int length, int packetId)
        {
            return (length, packetId) switch
            {
                (1, 0) => new Request(),
                (_, 1) => new Ping(Payload: reader.ReadLong()),
                _ => null
            };
        }
    }
}

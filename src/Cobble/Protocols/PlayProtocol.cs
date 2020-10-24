using Cobble.Packets;
using System;
using System.Buffers;

namespace Cobble.Protocols
{
    public class PlayProtocol : BaseProtocol
    {
        public override Packet GetPacket(ref SequenceReader<byte> reader, int length, int packetId)
        {
            throw new NotImplementedException();
        }
    }
}

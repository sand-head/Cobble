using Cobble.Extensions;
using Cobble.Packets;
using System.Buffers;

namespace Cobble.Protocols
{
    public class LoginProtocol : BaseProtocol
    {
        public override Packet GetPacket(ref SequenceReader<byte> reader, int length, int packetId)
        {
            return packetId switch
            {
                0 => new LoginStart(Username: reader.ReadString()),
                1 => null, // new EncryptionResponse(),
                2 => null, // new LoginPluginResponse(),
                _ => null
            };
        }
    }
}

using Microsoft.Extensions.Logging;
using ProjectBedrock.Minecraft.Extensions;
using ProjectBedrock.Minecraft.Packets;
using System.Buffers;

namespace ProjectBedrock.Minecraft
{
    public interface IPacketParser
    {
        bool TryParsePacket(ref ReadOnlySequence<byte> buffer, out Packet message);
    }

    public class PacketParser : IPacketParser
    {
        private readonly ILogger<PacketParser> _logger;

        public PacketParser(ILogger<PacketParser> logger)
        {
            _logger = logger;
        }

        public bool TryParsePacket(ref ReadOnlySequence<byte> buffer, out Packet message)
        {
            var reader = new SequenceReader<byte>(buffer);
            var length = reader.ReadVarInt();
            var packetId = reader.ReadVarInt();
            _logger.LogInformation("length: {Length}, packet ID: {PacketId}", length, packetId);

            message = packetId switch
            {
                0 => new Handshake(
                    ProtocolVersion: reader.ReadVarInt(),
                    Address: reader.ReadString(),
                    Port: reader.ReadUShort(),
                    State: reader.ReadVarInt()),
                // todo: add other packets to parse
                _ => null
            };

            return message != null;
        }
    }
}

using Microsoft.Extensions.Logging;
using Cobble.Minecraft.Extensions;
using Cobble.Minecraft.Packets;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace Cobble.Minecraft
{
    public interface IPacketParser
    {
        bool TryParsePackets(ref ReadOnlySequence<byte> buffer, out List<Packet> packets);
    }

    public class PacketParser : IPacketParser
    {
        private readonly ILogger<PacketParser> _logger;

        public PacketParser(ILogger<PacketParser> logger)
        {
            _logger = logger;
        }

        public bool TryParsePackets(ref ReadOnlySequence<byte> buffer, out List<Packet> packets)
        {
            var reader = new SequenceReader<byte>(buffer);
            packets = new List<Packet>();

            while (reader.Remaining != 0)
            {
                var length = reader.ReadVarInt();
                var packetId = reader.ReadVarInt();
                _logger.LogInformation("length: {Length}, packet ID: {PacketId}", length, packetId);

                packets.Add((length, packetId) switch
                {
                    (1, 0) => new Request(),
                    (_, 0) => new Handshake(
                        ProtocolVersion: reader.ReadVarInt(),
                        Address: reader.ReadString(),
                        Port: reader.ReadUShort(),
                        State: reader.ReadVarInt()),
                    // todo: add other packets to parse
                    _ => null
                });
            }

            return packets.Any();
        }
    }
}

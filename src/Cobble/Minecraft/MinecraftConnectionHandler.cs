using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Cobble.Minecraft.Packets;
using System;
using System.Threading.Tasks;

namespace Cobble.Minecraft
{
    public class MinecraftConnectionHandler : ConnectionHandler
    {
        private readonly ILogger<MinecraftConnectionHandler> _logger;
        private readonly IPacketParser _parser;

        public MinecraftConnectionHandler(ILogger<MinecraftConnectionHandler> logger, IPacketParser parser)
        {
            _logger = logger;
            _parser = parser;
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            var input = connection.Transport.Input;
            _logger.LogInformation("{ConnectionId} connected", connection.ConnectionId);

            while (!connection.ConnectionClosed.IsCancellationRequested)
            {
                var result = await input.ReadAsync();
                var buffer = result.Buffer;

                if (_parser.TryParsePackets(ref buffer, out var packets))
                {
                    foreach (var packet in packets)
                    {
                        var response = ProcessPacket(packet);
                        if (response != null)
                        {
                            // todo: write response to output
                        }
                    }
                }

                input.AdvanceTo(buffer.Start, buffer.End);
            }

            _logger.LogInformation("{ConnectionId} disconnected", connection.ConnectionId);
        }

        private Packet ProcessPacket(Packet packet)
        {
            _logger.LogInformation("packet: {Packet}", packet);
            return packet switch
            {
                Request => new Response(""),
                _ => null
            };
        }
    }
}

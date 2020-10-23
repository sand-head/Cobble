using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Cobble.Packets;
using System.Threading.Tasks;
using Cobble.Models;
using System.Collections.Generic;
using Bedrock.Framework.Protocols;

namespace Cobble
{
    public class MinecraftConnectionHandler : ConnectionHandler
    {
        private readonly ILogger<MinecraftConnectionHandler> _logger;

        public MinecraftConnectionHandler(ILogger<MinecraftConnectionHandler> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            _logger.LogInformation("{ConnectionId} connected", connection.ConnectionId);

            var protocol = new MinecraftProtocol();
            var reader = connection.CreateReader();
            var writer = connection.CreateWriter();

            while (!connection.ConnectionClosed.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(protocol);
                var packet = result.Message;

                _logger.LogInformation("Received: {Packet}", packet);

                if (result.IsCompleted)
                {
                    break;
                }

                reader.Advance();
            }

            _logger.LogInformation("{ConnectionId} disconnected", connection.ConnectionId);
        }

        private Packet ProcessPacket(Packet packet)
        {
            _logger.LogInformation("packet: {Packet}", packet);
            return packet switch
            {
                // todo: don't hardcode this response
                Request => new Response(new ResponsePayload(
                    Version: new ResponseVersion("1.16.3", 753),
                    Players: new ResponsePlayers(69, 0, new List<SamplePlayer>()),
                    Description: new ResponseDescription("this was a triumph"),
                    Favicon: ""
                    )),
                _ => null
            };
        }
    }
}

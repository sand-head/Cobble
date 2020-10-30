using Cobble.Models;
using Cobble.Packets;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cobble
{
    public class CobbleConnectionHandler : ConnectionHandler
    {
        private readonly ILogger<CobbleConnectionHandler> _logger;

        public CobbleConnectionHandler(ILogger<CobbleConnectionHandler> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync(ConnectionContext context)
        {
            _logger.LogInformation("{ConnectionId} connected", context.ConnectionId);
            var connection = new CobbleConnection(context);

            while (!context.ConnectionClosed.IsCancellationRequested)
            {
                var packet = await connection.ReadAsync();
                _logger.LogDebug("Received: {Packet}", packet);

                if (packet != null && TryProcessPacket(packet, out var response))
                {
                    _logger.LogDebug("Sending: {Packet}", response);
                    await connection.WriteAsync(response);
                }
            }

            _logger.LogInformation("{ConnectionId} disconnected", context.ConnectionId);
        }

        private bool TryProcessPacket(Packet packet, out Packet response)
        {
            response = packet switch
            {
                // todo: don't hardcode this response
                Request => new Response(new ResponsePayload(
                    Version: new ResponseVersion("1.16.3", 753),
                    Players: new ResponsePlayers(69, 0, new List<SamplePlayer>()),
                    Description: new ResponseDescription("this was a triumph"),
                    Favicon: null
                    )),
                Ping(long payload) => new Pong(payload),
                LoginStart(string username) => new LoginSuccess(Guid.NewGuid(), username),
                _ => null
            };
            return response != null;
        }
    }
}

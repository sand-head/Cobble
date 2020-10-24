using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Cobble.Packets;
using System.Threading.Tasks;
using Cobble.Models;
using System.Collections.Generic;
using Bedrock.Framework.Protocols;
using Cobble.Protocols;
using System;

namespace Cobble
{
    public class MinecraftConnectionHandler : ConnectionHandler
    {
        private readonly ILogger<MinecraftConnectionHandler> _logger;

        private readonly HandshakingProtocol _handshakingProtocol;
        private readonly StatusProtocol _statusProtocol;
        private readonly LoginProtocol _loginProtocol;
        private readonly PlayProtocol _playProtocol;
        private readonly Dictionary<string, PacketState> _clientStates;

        public MinecraftConnectionHandler(ILogger<MinecraftConnectionHandler> logger)
        {
            _logger = logger;

            _handshakingProtocol = new HandshakingProtocol();
            _statusProtocol = new StatusProtocol();
            _loginProtocol = new LoginProtocol();
            _playProtocol = new PlayProtocol();
            _clientStates = new Dictionary<string, PacketState>();
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            _logger.LogInformation("{ConnectionId} connected", connection.ConnectionId);
            _clientStates.Add(connection.ConnectionId, PacketState.Handshaking);

            var reader = connection.CreateReader();
            var writer = connection.CreateWriter();

            var handshakeResult = await reader.ReadAsync(_handshakingProtocol);
            var handshake = handshakeResult.Message;
            if (handshake is not Handshake)
            {
                connection.Abort(new ConnectionAbortedException("Did not recognize the incoming handshake."));
            }
            _clientStates[connection.ConnectionId] = ((Handshake)handshake).NextState;
            reader.Advance();

            while (!connection.ConnectionClosed.IsCancellationRequested)
            {
                try
                {
                    var protocol = GetProtocol(_clientStates[connection.ConnectionId]);
                    var result = await reader.ReadAsync(protocol);
                    var packet = result.Message;

                    _logger.LogInformation("Received: {Packet}", packet);

                    if (packet != null && TryProcessPacket(packet, out var response))
                    {
                        _logger.LogInformation("Sending: {Packet}", response);
                        await writer.WriteAsync(protocol, response);
                    }
                }
                finally
                {
                    reader.Advance();
                }
            }

            _logger.LogInformation("{ConnectionId} disconnected", connection.ConnectionId);
            _clientStates.Remove(connection.ConnectionId);
        }

        private BaseProtocol GetProtocol(PacketState state)
        {
            return state switch
            {
                PacketState.Handshaking => _handshakingProtocol,
                PacketState.Status => _statusProtocol,
                PacketState.Login => _loginProtocol,
                PacketState.Play => _playProtocol,
                _ => throw new NotImplementedException(),
            };
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

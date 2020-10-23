﻿using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using ProjectBedrock.Minecraft.Packets;
using System;
using System.Threading.Tasks;

namespace ProjectBedrock.Minecraft
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

                if (_parser.TryParsePacket(ref buffer, out var message))
                {
                    await ProcessPacketAsync(message);
                }

                input.AdvanceTo(buffer.Start, buffer.End);
            }

            _logger.LogInformation("{ConnectionId} disconnected", connection.ConnectionId);
        }

        private Task ProcessPacketAsync(Packet packet)
        {
            _logger.LogInformation("packet: {Packet}", packet);
            // todo: process packets
            throw new NotImplementedException();
        }
    }
}

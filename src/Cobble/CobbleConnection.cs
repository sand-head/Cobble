using Bedrock.Framework.Protocols;
using Cobble.Packets;
using Cobble.Protocols;
using Microsoft.AspNetCore.Connections;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Cobble
{
    public enum ConnectionState
    {
        Handshaking = 0,
        Status,
        Login,
        Play
    }

    public class CobbleConnection
    {
        private readonly ConnectionContext _context;
        private readonly ProtocolReader _reader;
        private readonly ProtocolWriter _writer;

        private BaseProtocol _protocol;

        public CobbleConnection(ConnectionContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _reader = context.CreateReader();
            _writer = context.CreateWriter();

            _protocol = new HandshakingProtocol();
        }

        public IPEndPoint RemoteEndPoint => _context.RemoteEndPoint as IPEndPoint;

        public ConnectionState State => _protocol switch {
            HandshakingProtocol => ConnectionState.Handshaking,
            StatusProtocol => ConnectionState.Status,
            LoginProtocol => ConnectionState.Login,
            PlayProtocol => ConnectionState.Play,
            _ => throw new MemberAccessException("Unknown protocol in use, current connection state is indeterminable.")
        };

        public async Task<Packet> ReadAsync()
        {
            try
            {
                var result = await _reader.ReadAsync(_protocol);
                var packet = result.Message;

                if (packet is Handshake handshake)
                {
                    SetState(handshake.NextState);
                }

                return packet;
            }
            finally
            {
                _reader.Advance();
            }
        }

        public async Task WriteAsync(Packet packet)
        {
            if (packet is LoginSuccess)
            {
                SetState(ConnectionState.Play);
            }

            await _writer.WriteAsync(_protocol, packet);
        }

        public void Close(string message)
        {
            _context.Abort(new ConnectionAbortedException(message));
        }

        private void SetState(ConnectionState state)
        {
            _protocol = state switch
            {
                ConnectionState.Status => new StatusProtocol(),
                ConnectionState.Login => new LoginProtocol(),
                ConnectionState.Play => new PlayProtocol(),
                _ => _protocol
            };
        }
    }
}

using Cobble.Extensions;
using Cobble.Models;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text.Json;

namespace Cobble.Packets
{
    public abstract record Packet()
    {
        public virtual void Write(IBufferWriter<byte> writer) =>
            throw new NotImplementedException("Writing this packet type is not intended.");
    }

    public record Handshake(int ProtocolVersion, string Address, ushort Port, int State) : Packet();

    #region Status packets

    public record Ping(long Payload) : Packet();
    public record Pong(long Payload) : Packet()
    {
        public override void Write(IBufferWriter<byte> writer)
        {
            var buffer = writer.GetSpan();
            BinaryPrimitives.WriteInt64BigEndian(buffer, Payload);
            writer.Advance(sizeof(long));
        }
    }

    public record Request() : Packet();
    public record Response(ResponsePayload JsonResponse) : Packet()
    {
        public override void Write(IBufferWriter<byte> writer)
        {
            var jsonResponse = JsonSerializer.Serialize(JsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                IgnoreNullValues = true
            });
            // todo: figure out writing packet headers (length and ID)
            var buffer = writer.GetSpan(5 + jsonResponse.Length);
            var length = buffer.WriteString(jsonResponse);
            writer.Advance(length);
        }
    }

    #endregion

    #region Login packets

    public record LoginStart(string Username) : Packet();
    public record EncryptionResponse(int SharedSecretLength, byte[] SharedSecret, int VerifyTokenLength, byte[] VerifyToken) : Packet();
    public record LoginPluginResponse(int MessageId, bool Successful, byte[] Data) : Packet();

    #endregion
}

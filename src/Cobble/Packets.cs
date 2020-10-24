using Cobble.Extensions;
using Cobble.Models;
using System;
using System.Buffers.Binary;
using System.Text.Json;

namespace Cobble.Packets
{
    public abstract record Packet(int PacketId)
    {
        // note: please make sure that the lengths of the returned spans are trimmed properly
        // this is really just a reminder to myself
        public virtual Span<byte> ToSpan() =>
            throw new NotImplementedException("Converting this packet type to Span is not intended.");
    }

    public record Handshake(int ProtocolVersion, string Address, ushort Port, int State) : Packet(0);

    #region Status packets

    public record Ping(long Payload) : Packet(1);
    public record Pong(long Payload) : Packet(1)
    {
        public override Span<byte> ToSpan()
        {
            var payloadBuffer = new Span<byte>(new byte[sizeof(long)]);
            BinaryPrimitives.WriteInt64BigEndian(payloadBuffer, Payload);
            return payloadBuffer;
        }
    }

    public record Request() : Packet(0);
    public record Response(ResponsePayload JsonResponse) : Packet(0)
    {
        public override Span<byte> ToSpan()
        {
            var jsonResponse = JsonSerializer.Serialize(JsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                IgnoreNullValues = true
            });
            
            var payloadBuffer = new Span<byte>(new byte[5 + jsonResponse.Length]);
            var payloadLen = payloadBuffer.WriteString(jsonResponse);
            return payloadBuffer[0..payloadLen];
        }
    }

    #endregion

    #region Login packets

    public record LoginStart(string Username) : Packet(0);
    public record EncryptionResponse(int SharedSecretLength, byte[] SharedSecret, int VerifyTokenLength, byte[] VerifyToken) : Packet(1);
    public record LoginPluginResponse(int MessageId, bool Successful, byte[] Data) : Packet(2);

    #endregion
}

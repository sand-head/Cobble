using System;
using System.Buffers;

namespace Cobble.Minecraft.Packets
{
    public abstract record Packet()
    {
        public virtual void Write(IBufferWriter<byte> writer) =>
            throw new NotImplementedException("Writing this packet type is not intended.");
    }

    public record Ping(long Payload) : Packet();
    public record Pong(long Payload) : Packet();

    public record Request() : Packet();
    public record Response(string JsonResponse) : Packet()
    {
        public override void Write(IBufferWriter<byte> writer)
        {
            base.Write(writer);
        }
    }

    public record Handshake(int ProtocolVersion, string Address, ushort Port, int State) : Packet();
}

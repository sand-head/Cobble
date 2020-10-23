namespace ProjectBedrock.Minecraft.Packets
{
    public abstract record Packet();

    public record Ping(long Payload) : Packet();
    public record Pong(long Payload) : Packet();

    public record Request() : Packet();
    public record Response(string JsonResponse) : Packet();

    public record Handshake(int ProtocolVersion, string Address, ushort Port, int State) : Packet();
}

namespace ProjectBedrock.Minecraft.Packets
{
    public record Handshake(int ProtocolVersion, string Address, ushort Port, int State) : Packet();
}

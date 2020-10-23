using System;
using System.Collections.Generic;

namespace Cobble.Models
{
    public record ResponseVersion(string Name, int Protocol);
    public record SamplePlayer(string Name, Guid Id);
    public record ResponsePlayers(int Max, int Online, List<SamplePlayer> Sample);
    public record ResponseDescription(string Text);

    public record ResponsePayload(ResponseVersion Version, ResponsePlayers Players, ResponseDescription Description, string Favicon);
}

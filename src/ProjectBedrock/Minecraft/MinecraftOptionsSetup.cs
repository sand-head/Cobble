using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace ProjectBedrock.Minecraft
{
    public class MinecraftOptionsSetup : IConfigureOptions<KestrelServerOptions>
    {
        private readonly MinecraftOptions _options;

        public MinecraftOptionsSetup(IOptions<MinecraftOptions> options)
        {
            _options = options.Value;
        }

        public void Configure(KestrelServerOptions options)
        {
            options.Listen(_options.EndPoint, builder =>
            {
                builder.UseConnectionHandler<MinecraftConnectionHandler>();
            });
        }
    }
}

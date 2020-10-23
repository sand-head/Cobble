using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Net;

namespace ProjectBedrock.Minecraft.Extensions
{
    public static class IServiceProviderExtensions
    {
        public static IServiceCollection AddMinecraft(this IServiceCollection services, IPEndPoint endpoint = null)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<KestrelServerOptions>, MinecraftOptionsSetup>());

            services.Configure<MinecraftOptions>(options =>
            {
                options.EndPoint = endpoint ?? new IPEndPoint(IPAddress.Loopback, 25565);
            });

            services.TryAddSingleton<IPacketParser, PacketParser>();
            return services;
        }
    }
}

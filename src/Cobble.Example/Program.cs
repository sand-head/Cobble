using Cobble.Extensions;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Net;

WebHost.CreateDefaultBuilder(args)
    .ConfigureKestrel(options =>
    {
        // configures kestrel to also listen to ASP.NET Core requests
        options.Listen(new IPEndPoint(IPAddress.Loopback, 5000));
    })
    .ConfigureServices(services =>
    {
        services.AddMinecraft();
    })
    .Configure((app) =>
    {
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/", async context =>
            {
                await context.Response.WriteAsync("Hello World!");
            });
        });
    })
    .Build()
    .Run();
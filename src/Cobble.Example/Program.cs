using Cobble.Extensions;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

WebHost.CreateDefaultBuilder(args)
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
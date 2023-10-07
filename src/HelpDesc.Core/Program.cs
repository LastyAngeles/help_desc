using HelpDesc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;
        services.Configure<TeamsConfig>(config.GetSection("TeamsConfig"));
    })
    .UseOrleans(siloBuilder =>
    {
        siloBuilder.UseLocalhostClustering()
            .AddMemoryGrainStorage("PubSubStore")
            .AddMemoryStreams("desc");
    })
    .RunConsoleAsync();
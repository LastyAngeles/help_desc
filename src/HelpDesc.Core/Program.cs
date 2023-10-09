using HelpDesc.Core;
using HelpDesc.Core.Extensions;
using HelpDesc.Core.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;
        services.Configure<TeamsConfig>(config.GetSection("TeamsConfig"));
        services.AddSingleton<ITimeProvider, TimeProvider>();
    })
    .UseOrleans(siloBuilder =>
    {
        siloBuilder.UseLocalhostClustering()
            .AddMemoryStreams(SolutionConst.StreamProviderName)
            .AddMemoryGrainStorage("PubSubStore")
            .AddMemoryGrainStorage(SolutionConst.HelpDescStore)
            .UseInMemoryReminderService();
    })
    .RunConsoleAsync();
using HelpDesc.Core;
using HelpDesc.Core.Extensions;
using HelpDesc.Core.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Orleans.Hosting;


await Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;
        services.Configure<TeamsConfig>(config.GetSection("TeamsConfig"));
        services.Configure<Intervals>(config.GetSection("Intervals"));
        services.Configure<OrleansPersistence>(config.GetSection("Orleans").GetSection("Persistence"));
        services.AddSingleton<ITimeProvider, TimeProvider>();

#if DEBUG
        services.AddHostedService<DataBaseInitService>();
#endif
    })
    .UseOrleans((context, siloBuilder) =>
    {
        var orleansSection = context.Configuration.GetSection("Orleans");

        var persistence = orleansSection.GetSection("Persistence").Get<OrleansPersistence>();
        var endpoints = orleansSection.GetSection("Endpoints").Get<Endpoints>();
        var postgresInvariant = "Npgsql";

        siloBuilder
            .Configure<ClusterOptions>(orleansSection.GetSection("Cluster"))
            .Configure<GrainCollectionOptions>(orleansSection.GetSection("GrainCollection"))
            .ConfigureEndpoints(endpoints.SiloPort, endpoints.GatewayPort, listenOnAnyHostAddress: true)
            .UseAdoNetClustering(options =>
            {
                options.Invariant = postgresInvariant;
                options.ConnectionString = persistence.ConnectionString;
                //options.UseJsonFormat = true;
            })
            .AddMemoryStreams(SolutionConst.StreamProviderName)
            .AddAdoNetGrainStorage("PubSubStore", options =>
            {
                options.Invariant = postgresInvariant;
                options.ConnectionString = persistence.ConnectionString;
                //options.UseJsonFormat = true;
            })
            .AddAdoNetGrainStorage(SolutionConst.HelpDescStore, options =>
            {
                options.Invariant = postgresInvariant;
                options.ConnectionString = persistence.ConnectionString;
                //options.UseJsonFormat = true;
            })
            .UseAdoNetReminderService(options =>
            {
                options.Invariant = postgresInvariant;
                options.ConnectionString = persistence.ConnectionString;
                //options.UseJsonFormat = true;
            });
    })
    .RunConsoleAsync();
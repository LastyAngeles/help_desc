using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Orleans.Hosting;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using HelpDesc.Host;
using System.Threading.Tasks;
using System.Threading;
using System;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrleansClient((context, clientBuilder) =>
{
    var orleansSection = context.Configuration.GetSection("Orleans");

    var persistence = orleansSection.GetSection("Persistence").Get<OrleansPersistence>();
    var connection = orleansSection.GetSection("Connection").Get<OrleansConnection>();
    var postgresInvariant = "Npgsql";
    var attempt = 0;

    clientBuilder
        .UseConnectionRetryFilter(RetryFilter)
        .Configure<ClusterOptions>(orleansSection.GetSection("Cluster"))
        .UseAdoNetClustering(options =>
        {
            options.Invariant = postgresInvariant;
            options.ConnectionString = persistence.ConnectionString;
        })
        // TODO: move to consts (Maxim Meshkov 2023-10-10)
        .AddMemoryStreams("HelpDesc");

    async Task<bool> RetryFilter(Exception exception, CancellationToken cancellationToken)
    {
        attempt++;
        Console.WriteLine(
            $"Cluster client attempt {attempt} of {connection.MaxAttempts} failed to connect to cluster.  Exception: {exception}");
        if (attempt > connection.MaxAttempts)
            return false;

        await Task.Delay(connection.RetryDelay, cancellationToken);
        return true;
    }
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
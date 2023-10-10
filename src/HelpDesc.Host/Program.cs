using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Orleans.Hosting;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using HelpDesc.Host;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrleansClient((context, clientBuilder) =>
{
    var orleansSection = context.Configuration.GetSection("Orleans");

    var persistence = orleansSection.GetSection("Persistence").Get<OrleansPersistence>();
    var postgresInvariant = "Npgsql";

    clientBuilder
        .Configure<ClusterOptions>(orleansSection.GetSection("Cluster"))
        .UseAdoNetClustering(options =>
        {
            options.Invariant = postgresInvariant;
            options.ConnectionString = persistence.ConnectionString;
        })
        // TODO: move to consts (Maxim Meshkov 2023-10-10)
        .AddMemoryStreams("HelpDesc");
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
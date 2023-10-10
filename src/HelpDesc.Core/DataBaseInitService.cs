using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace HelpDesc.Core;

public class DataBaseInitService : IHostedService
{
    private readonly OrleansPersistence orleansPersistence;
    private const string MainSql = "Scripts/PostgreSQL-Main.sql";
    private const string PersistenceSql = "Scripts/PostgreSQL-Persistence.sql";
    private const string ClusteringMigrationSql = "Scripts/PostgreSQL-Clustering-3.7.0.sql";
    private const string ClusteringSql = "Scripts/PostgreSQL-Clustering.sql";
    private const string RemindersSql = "Scripts/PostgreSQL-Reminders.sql";

    private const string ExistCommand = @"
SELECT EXISTS (
    SELECT FROM 
        pg_tables
    WHERE 
        schemaname = 'public' AND 
        tablename  = 'orleansquery'
    );";

    public DataBaseInitService(IOptions<OrleansPersistence> orleansPersistence)
    {
        this.orleansPersistence = orleansPersistence.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = NpgsqlDataSource.Create(orleansPersistence.ConnectionString);

        var exists = false;
        try
        {
            await using var command = dataSource.CreateCommand(ExistCommand);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                exists = reader.GetBoolean(0);
            }
        }
        catch (NpgsqlException)
        {
            // i.e. database is not connected
            throw;
        }

        if (!exists)
        {
            await RunSql(dataSource, MainSql, cancellationToken);
            await RunSql(dataSource, PersistenceSql, cancellationToken);
            await RunSql(dataSource, ClusteringMigrationSql, cancellationToken);
            await RunSql(dataSource, ClusteringSql, cancellationToken);
            await RunSql(dataSource, RemindersSql, cancellationToken);
        }
    }

    private async Task RunSql(NpgsqlDataSource dataSource, string sqlPath, CancellationToken cancellationToken)
    {
        var commandText = await File.ReadAllTextAsync(sqlPath, cancellationToken);
        await using var command = dataSource.CreateCommand(commandText);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            Console.WriteLine(reader.GetString(0));
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
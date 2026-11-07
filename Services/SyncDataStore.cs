using System.Data;
using KimiSync.Models;
using Microsoft.Data.SqlClient;

namespace KimiSync.Services;

public sealed class SyncDataStore
{
    private readonly string connectionString;

    public SyncDataStore(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("KimiSync")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:KimiSync for SQL Server.");
    }

    public async Task Initialize(CancellationToken cancellationToken)
    {
        using var stream = typeof(SyncDataStore).Assembly.GetManifestResourceStream("KimiSync.Database.schema.sql")!;
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(cancellationToken);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SyncData?> Get(string id, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT Id, Content, CreatedAt, UpdatedAt, Version FROM dbo.SyncData WHERE Id = @id", connection);
        command.Parameters.Add("@id", SqlDbType.VarChar, 36).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<SyncData> Put(string id, string content, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        // Range locks serialize concurrent inserts for the same client, including an absent row.
        const string sql = """
            DECLARE @now datetime2(3) = SYSUTCDATETIME();
            UPDATE dbo.SyncData WITH (UPDLOCK, HOLDLOCK)
            SET Content = @content, UpdatedAt = @now WHERE Id = @id;
            IF @@ROWCOUNT = 0
                INSERT dbo.SyncData (Id, Content, CreatedAt, UpdatedAt, Version)
                VALUES (@id, @content, @now, @now, 0);
            SELECT Id, Content, CreatedAt, UpdatedAt, Version FROM dbo.SyncData WHERE Id = @id;
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id", SqlDbType.VarChar, 36).Value = id;
        command.Parameters.Add("@content", SqlDbType.NVarChar, -1).Value = content;
        SyncData result;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            result = Read(reader);
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static SyncData Read(SqlDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Content = reader.GetString(1),
        CreatedAt = DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc),
        UpdatedAt = DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc),
        Version = reader.IsDBNull(4) ? null : reader.GetInt32(4)
    };
}

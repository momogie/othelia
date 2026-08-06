using Dapper;
using Microsoft.Data.SqlClient;

namespace Othelia.Api.Observability;

public interface ISchemaInitializer
{
    Task EnsureSchemaAsync(CancellationToken ct);
}

public sealed class SchemaInitializer : ISchemaInitializer
{
    private readonly string _connectionString;

    public SchemaInitializer(string connectionString) => _connectionString = connectionString;

    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        const string sql = @"
IF OBJECT_ID(N'dbo.Settings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Settings
    (
        [Key]   NVARCHAR(200)  NOT NULL CONSTRAINT PK_Settings PRIMARY KEY,
        [Value] NVARCHAR(MAX)   NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.Spans', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Spans
    (
        Id                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Spans PRIMARY KEY,
        TraceId           CHAR(32)   NOT NULL,
        SpanId            CHAR(16)   NOT NULL,
        ParentSpanId      CHAR(16)   NULL,
        [Name]            NVARCHAR(1024) NOT NULL,
        ServiceName       NVARCHAR(255)  NOT NULL,
        Kind              NVARCHAR(16)   NOT NULL,
        StartTimeUtc      DATETIME2(3)   NOT NULL,
        EndTimeUtc        DATETIME2(3)   NOT NULL,
        DurationUs        BIGINT     NOT NULL,
        StatusCode        NVARCHAR(16)   NOT NULL,
        StatusMessage     NVARCHAR(1024) NULL,
        HttpMethod        NVARCHAR(16)   NULL,
        HttpPath          NVARCHAR(1024) NULL,
        HttpStatusCode    INT        NULL,
        ServiceVersion    NVARCHAR(64)   NULL,
        ServiceEnvironment NVARCHAR(64)  NULL,
        ResourceJson      NVARCHAR(MAX)  NULL,
        AttributesJson    NVARCHAR(MAX)  NULL,
        EventsJson        NVARCHAR(MAX)  NULL,
        LinksJson         NVARCHAR(MAX)  NULL
    );

    CREATE INDEX IX_Spans_TraceId ON dbo.Spans (TraceId) INCLUDE (StartTimeUtc);
    CREATE INDEX IX_Spans_StartTimeUtc ON dbo.Spans (StartTimeUtc) INCLUDE (TraceId, StatusCode);
    CREATE INDEX IX_Spans_ServiceName_StartTimeUtc ON dbo.Spans (ServiceName, StartTimeUtc);
END;

IF OBJECT_ID(N'dbo.Logs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Logs
    (
        Id                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Logs PRIMARY KEY,
        TimestampUtc      DATETIME2(3)   NOT NULL,
        ServiceName       NVARCHAR(255)  NOT NULL,
        ServiceVersion    NVARCHAR(64)   NULL,
        ServiceEnvironment NVARCHAR(64)  NULL,
        SeverityText      NVARCHAR(32)   NOT NULL,
        SeverityNumber    INT            NOT NULL,
        Body              NVARCHAR(MAX)  NULL,
        TraceId           CHAR(32)       NULL,
        SpanId            CHAR(16)       NULL,
        AttributesJson    NVARCHAR(MAX)  NULL,
        ResourceJson      NVARCHAR(MAX)  NULL
    );

    CREATE INDEX IX_Logs_TimestampUtc ON dbo.Logs (TimestampUtc) INCLUDE (ServiceName, SeverityText);
    CREATE INDEX IX_Logs_ServiceName_TimestampUtc ON dbo.Logs (ServiceName, TimestampUtc);
END;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }
}

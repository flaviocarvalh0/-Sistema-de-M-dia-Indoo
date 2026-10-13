using System.Data;
using Atlas.Application.Common.Interfaces;
using Npgsql;

namespace Atlas.Infrastructure.Data;

public class DapperContext : IDapperContext
{
    private readonly string _connectionString;

    // Recebe a string de conexão diretamente, injetada pelo DI
    public DapperContext(string connectionString)
        => _connectionString = connectionString;

    public IDbConnection CreateConnection()
        => new NpgsqlConnection(_connectionString);
}

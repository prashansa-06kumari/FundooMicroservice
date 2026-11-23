using Npgsql;

namespace Fundoo.Notes.Infrastructure.Data;

public sealed class NotesConnectionFactory
{
    private readonly string _connectionString;

    public NotesConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "Database connection string is missing.",
                nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}
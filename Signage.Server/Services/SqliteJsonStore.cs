using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Signage.Server.Services;

public class SqliteJsonStore
{
    private readonly string _connectionString;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _sync = new();

    public SqliteJsonStore(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration.GetConnectionString("SignageDb") ?? "Data Source=Data/signage.db";
        var builder = new SqliteConnectionStringBuilder(configured);

        if (!string.IsNullOrWhiteSpace(builder.DataSource) && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.Combine(environment.ContentRootPath, builder.DataSource);
        }

        var directory = Path.GetDirectoryName(builder.DataSource);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = builder.ToString();
        EnsureCreated();
    }

    public List<T> LoadAll<T>(string category)
    {
        lock (_sync)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Json
                FROM JsonRecords
                WHERE Category = $category
                ORDER BY UpdatedAtUtc, Id;
                """;
            command.Parameters.AddWithValue("$category", category);

            using var reader = command.ExecuteReader();
            var items = new List<T>();
            while (reader.Read())
            {
                var json = reader.GetString(0);
                var item = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                if (item is not null)
                {
                    items.Add(item);
                }
            }

            return items;
        }
    }

    public void ReplaceAll<T>(string category, IEnumerable<T> items, Func<T, string> getId)
    {
        lock (_sync)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using (var deleteCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = "DELETE FROM JsonRecords WHERE Category = $category;";
                deleteCommand.Parameters.AddWithValue("$category", category);
                deleteCommand.ExecuteNonQuery();
            }

            foreach (var item in items)
            {
                using var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText = """
                    INSERT INTO JsonRecords (Category, Id, Json, UpdatedAtUtc)
                    VALUES ($category, $id, $json, $updatedAtUtc);
                    """;
                insertCommand.Parameters.AddWithValue("$category", category);
                insertCommand.Parameters.AddWithValue("$id", getId(item));
                insertCommand.Parameters.AddWithValue("$json", JsonSerializer.Serialize(item, _jsonOptions));
                insertCommand.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O"));
                insertCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    private void EnsureCreated()
    {
        lock (_sync)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS JsonRecords (
                    Category TEXT NOT NULL,
                    Id TEXT NOT NULL,
                    Json TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL,
                    PRIMARY KEY (Category, Id)
                );
                """;
            command.ExecuteNonQuery();
        }
    }
}
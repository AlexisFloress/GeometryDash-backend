using System.Reflection;
using GeometryDash.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
namespace GeometryDash.Api.Data;
public static class DatabaseMigrations
{
    public static async Task Apply(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(712345601)");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"__GeometryMigrations\" (\"Name\" text PRIMARY KEY, \"Hash\" text NOT NULL, \"AppliedAt\" timestamptz NOT NULL DEFAULT now())");
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)!; using var reader = new StreamReader(stream);
            var sql = (await reader.ReadToEndAsync()).Replace("\r\n", "\n"); var hash = LevelRules.Hash(sql);
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = tx.GetDbTransaction(); command.CommandText = "SELECT \"Hash\" FROM \"__GeometryMigrations\" WHERE \"Name\" = @name";
            command.Parameters.Add(new NpgsqlParameter("name", name));
            var previous = await command.ExecuteScalarAsync();
            if (previous is string old) { if (old != hash) throw new InvalidOperationException($"Migración aplicada modificada: {name}"); continue; }
            await db.Database.ExecuteSqlRawAsync(sql);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"__GeometryMigrations\" (\"Name\", \"Hash\") VALUES ({name}, {hash})");
            Console.WriteLine($"Aplicada: {name}");
        }
        await tx.CommitAsync();
    }
}

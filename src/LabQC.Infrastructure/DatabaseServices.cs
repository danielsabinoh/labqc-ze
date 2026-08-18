using System.IO.Compression;
using System.Security.Cryptography;
using LabQC.Application;
using LabQC.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LabQC.Infrastructure;

public sealed class AuthenticationService(LabDbContext db)
{
    public async Task<User?> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Username == username && x.IsActive, ct);
        return user is not null && PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
    }
}

public sealed class AnalysisEntryService(LabDbContext db)
{
    public async Task<AnalysisResult> SaveCorrectionSafeAsync(Guid sampleId, Guid lotParameterId, decimal? numeric, string? text, bool? conformity, User user, string? reason, CancellationToken ct = default)
    {
        var lotStatus = await db.Samples.Where(x => x.Id == sampleId).Select(x => x.Lot.Status).SingleAsync(ct);
        if (lotStatus == LotStatus.Closed) throw new InvalidOperationException("O lote está fechado e não aceita lançamentos ou correções.");
        var previous = await db.AnalysisResults.Where(x => x.SampleId == sampleId && x.LotParameterId == lotParameterId && x.IsCurrent).SingleOrDefaultAsync(ct);
        if (previous is not null && string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A correção exige justificativa.");
        if (previous is not null) previous.IsCurrent = false;
        var result = new AnalysisResult { SampleId = sampleId, LotParameterId = lotParameterId, NumericValue = numeric, TextValue = text, ConformityValue = conformity, Version = (previous?.Version ?? 0) + 1, ReplacesResultId = previous?.Id, EnteredByUserId = user.Id, EnteredAt = DateTimeOffset.Now, CorrectionReason = reason };
        db.AnalysisResults.Add(result);
        db.AuditEntries.Add(new AuditEntry { UserId = user.Id, OccurredAt = result.EnteredAt, EntityName = nameof(AnalysisResult), EntityId = result.Id.ToString(), Action = previous is null ? "Criado" : "Corrigido", OldValue = previous is null ? null : $"{previous.NumericValue}|{previous.TextValue}|{previous.ConformityValue}", NewValue = $"{numeric}|{text}|{conformity}", Justification = reason });
        await db.SaveChangesAsync(ct);
        return result;
    }
}

public sealed class BackupService(string databasePath, string certificateDirectory)
{
    public async Task<string> CreateAsync(string destinationDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(destinationDirectory);
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var tempDb = Path.Combine(Path.GetTempPath(), $"labqc_{Guid.NewGuid():N}.db");
        var package = Path.Combine(destinationDirectory, $"LabQC_{stamp}.labbackup");
        try
        {
            await using var source = new SqliteConnection($"Data Source={databasePath}");
            await using var target = new SqliteConnection($"Data Source={tempDb}");
            await source.OpenAsync(ct); await target.OpenAsync(ct); source.BackupDatabase(target);
            await target.CloseAsync();
            using var zip = ZipFile.Open(package, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(tempDb, "data/labqc.db", CompressionLevel.Optimal);
            if (Directory.Exists(certificateDirectory))
                foreach (var file in Directory.EnumerateFiles(certificateDirectory, "*.pdf", SearchOption.AllDirectories))
                    zip.CreateEntryFromFile(file, "certificates/" + Path.GetRelativePath(certificateDirectory, file).Replace('\\', '/'), CompressionLevel.Optimal);
            var manifest = zip.CreateEntry("manifest.txt");
            await using var writer = new StreamWriter(manifest.Open());
            await writer.WriteAsync($"LabQC Backup\nCreated={DateTimeOffset.Now:O}\nFormat=1");
            return package;
        }
        finally { if (File.Exists(tempDb)) File.Delete(tempDb); }
    }

    public static async Task<bool> VerifyAsync(string package, CancellationToken ct = default)
    {
        using var zip = ZipFile.OpenRead(package);
        var dbEntry = zip.GetEntry("data/labqc.db") ?? throw new InvalidDataException("Banco ausente no backup.");
        var temp = Path.Combine(Path.GetTempPath(), $"verify_{Guid.NewGuid():N}.db");
        try
        {
            await using (var output = File.Create(temp)) await dbEntry.Open().CopyToAsync(output, ct);
            await using var connection = new SqliteConnection($"Data Source={temp};Mode=ReadOnly");
            await connection.OpenAsync(ct);
            var result = await new SqliteCommand("PRAGMA integrity_check;", connection).ExecuteScalarAsync(ct);
            return string.Equals(result?.ToString(), "ok", StringComparison.OrdinalIgnoreCase);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}

public static class DatabaseSeeder
{
    public static async Task SeedAsync(LabDbContext db)
    {
        await db.Database.MigrateAsync();
        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new User { Username = "admin", FullName = "Administrador", Role = UserRole.Administrator, PasswordHash = PasswordHasher.Hash("Admin@123"), MustChangePassword = true });
            await db.SaveChangesAsync();
        }
    }
}

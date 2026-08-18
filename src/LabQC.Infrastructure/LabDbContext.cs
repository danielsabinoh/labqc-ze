using LabQC.Domain;
using Microsoft.EntityFrameworkCore;

namespace LabQC.Infrastructure;

public sealed class LabDbContext(DbContextOptions<LabDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<AnalysisParameter> AnalysisParameters => Set<AnalysisParameter>();
    public DbSet<ParameterOption> ParameterOptions => Set<ParameterOption>();
    public DbSet<ProductSpecification> ProductSpecifications => Set<ProductSpecification>();
    public DbSet<ProductSpecificationParameter> ProductSpecificationParameters => Set<ProductSpecificationParameter>();
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<LotParameter> LotParameters => Set<LotParameter>();
    public DbSet<Sample> Samples => Set<Sample>();
    public DbSet<AnalysisResult> AnalysisResults => Set<AnalysisResult>();
    public DbSet<LotRelease> LotReleases => Set<LotRelease>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<CertificateResult> CertificateResults => Set<CertificateResult>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<BackupHistory> BackupHistory => Set<BackupHistory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("labqc");
        b.Entity<User>().HasIndex(x => x.Username).IsUnique();
        b.Entity<Product>().HasIndex(x => x.Code).IsUnique();
        b.Entity<AnalysisParameter>().HasIndex(x => x.Code).IsUnique();
        b.Entity<ProductSpecification>().HasIndex(x => new { x.ProductId, x.Version }).IsUnique();
        b.Entity<Lot>().HasIndex(x => new { x.ProductId, x.Number }).IsUnique();
        b.Entity<Sample>().HasIndex(x => new { x.LotId, x.Code }).IsUnique();
        b.Entity<Certificate>().HasIndex(x => new { x.Number, x.Version }).IsUnique();
        b.Entity<SystemSetting>().HasIndex(x => x.Key).IsUnique();

        b.Entity<ProductSpecification>().HasOne(x => x.Product).WithMany(x => x.Specifications).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProductSpecificationParameter>().HasOne(x => x.ProductSpecification).WithMany(x => x.Parameters).HasForeignKey(x => x.ProductSpecificationId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProductSpecificationParameter>().HasOne(x => x.AnalysisParameter).WithMany().HasForeignKey(x => x.AnalysisParameterId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Lot>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LotParameter>().HasOne(x => x.Lot).WithMany(x => x.Parameters).HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Sample>().HasOne(x => x.Lot).WithMany(x => x.Samples).HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AnalysisResult>().HasOne(x => x.Sample).WithMany(x => x.Results).HasForeignKey(x => x.SampleId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AnalysisResult>().HasOne(x => x.LotParameter).WithMany().HasForeignKey(x => x.LotParameterId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LotRelease>().HasOne(x => x.Lot).WithMany(x => x.Releases).HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CertificateResult>().HasOne(x => x.Certificate).WithMany(x => x.Results).HasForeignKey(x => x.CertificateId).OnDelete(DeleteBehavior.Restrict);

        foreach (var p in b.Model.GetEntityTypes().SelectMany(t => t.GetProperties()).Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            p.SetPrecision(18);
            p.SetScale(6);
        }
    }
}

public sealed class LabDbContextFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<LabDbContext>
{
    public LabDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LabDbContext>().UseSqlite("Data Source=labqc.db").Options;
        return new(options);
    }
}

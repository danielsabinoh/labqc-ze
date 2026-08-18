using LabQC.Application;
using LabQC.Domain;
using LabQC.Reports;
using LabQC.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LabQC.Tests;

public sealed class DomainTests
{
    [Fact] public void DecimalPtBrIsParsed() { Assert.True(BrazilianDecimal.TryParse("8,104", out var value)); Assert.Equal(8.104m, value); }

    [Fact] public void ProductDisplayNameIncludesCommercialUnit()
    {
        var product = new Product { Name = "Farinha de mandioca branca fina", CommercialUnit = "saco 50 kg" };
        Assert.Equal("Farinha de mandioca branca fina — saco 50 kg", product.DisplayName);
    }

    [Fact] public void TechnicalEnumsHavePortugueseLabels()
    {
        Assert.Equal("Físico-químico", PortugueseLabels.Category(ParameterCategory.Physicochemical));
        Assert.Equal("Numérico", PortugueseLabels.ResultType(ResultType.Numeric));
        Assert.Equal("Média", PortugueseLabels.Consolidation(ConsolidationMethod.Average));
    }

    [Fact] public void AverageUsesOriginalPrecision()
    {
        var p = new LotParameter { Id = Guid.NewGuid(), ResultType = ResultType.Numeric, ConsolidationMethod = ConsolidationMethod.Average, Maximum = 13m };
        var r = new[] { 8.104m, 7.943m, 8.021m }.Select((v, i) => new AnalysisResult { NumericValue = v, IsCurrent = true, IsValid = true, EnteredAt = DateTimeOffset.UtcNow.AddMinutes(i) });
        var result = ConsolidationEngine.Consolidate(p, r);
        Assert.Equal(8.022666666666666666666666667m, result.NumericValue);
        Assert.Equal(ConformityStatus.Conforming, result.Conformity);
    }

    [Fact] public void NonConformingIsDetected()
    {
        var p = new LotParameter { Id = Guid.NewGuid(), ResultType = ResultType.Numeric, ConsolidationMethod = ConsolidationMethod.Maximum, Maximum = 13m };
        var result = ConsolidationEngine.Consolidate(p, [new AnalysisResult { NumericValue = 14.2m }]);
        Assert.Equal(ConformityStatus.NonConforming, result.Conformity);
    }

    [Fact] public void LotKeepsSpecificationSnapshot()
    {
        var product = new Product { Id = Guid.NewGuid(), CommercialUnit = "sacas 50 kg", ShelfLifeMonths = 12 };
        var parameter = new AnalysisParameter { Id = Guid.NewGuid(), Code = "UMI", Name = "Umidade", Unit = "%", ResultType = ResultType.Numeric };
        var spec = new ProductSpecification { Id = Guid.NewGuid(), ProductId = product.Id, IsActive = true, Parameters = [new() { AnalysisParameterId = parameter.Id, AnalysisParameter = parameter, Maximum = 13m, ConsolidationMethod = ConsolidationMethod.Average }] };
        var lot = LotFactory.Create("280", product, spec, new DateOnly(2026, 5, 21), 100, "Produção", DateTimeOffset.UtcNow);
        spec.Parameters[0].Maximum = 12.5m;
        Assert.Equal(13m, lot.Parameters[0].Maximum);
    }

    [Fact] public void AnalystCannotReleaseLot()
    {
        var lot = new Lot { Status = LotStatus.AwaitingRelease };
        var analyst = new User { Role = UserRole.Analyst };
        Assert.Throws<UnauthorizedAccessException>(() => LotWorkflow.Transition(lot, LotStatus.Approved, analyst, "", DateTimeOffset.UtcNow));
    }

    [Fact] public void PasswordIsSaltedAndVerifiable()
    {
        var first = PasswordHasher.Hash("segredo"); var second = PasswordHasher.Hash("segredo");
        Assert.NotEqual(first, second); Assert.True(PasswordHasher.Verify("segredo", first)); Assert.False(PasswordHasher.Verify("errada", first));
    }

    [Fact] public void CertificatePdfIsGeneratedWithHash()
    {
        var certificate = new Certificate { Number = "2026-000001", Version = 1, ProductName = "Farinha Tipo 1", LotNumber = "280", ClientName = "Cliente Teste", City = "São Paulo", State = "SP", InvoiceNumber = "123", CertifiedQuantity = 50, QuantityUnit = "sacas", ManufactureDate = new DateOnly(2026, 5, 21), ExpiryDate = new DateOnly(2027, 5, 21), IssuedAt = DateTimeOffset.Now };
        certificate.Results.Add(new CertificateResult { ParameterName = "Umidade", Category = ParameterCategory.Physicochemical, Result = "8,02", Unit = "%", Specification = "Máx. 13,00", Conformity = ConformityStatus.Conforming });
        var directory = Path.Combine(Path.GetTempPath(), "LabQC.Tests", Guid.NewGuid().ToString("N"));
        try { var path = new CertificatePdfService().Generate(certificate, "EMPRESA TESTE", directory); Assert.True(File.Exists(path)); Assert.True(new FileInfo(path).Length > 500); Assert.Equal(64, certificate.PdfSha256.Length); }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact] public async Task LotStatusTransitionPersistsInSqlite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"labqc_transition_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<LabDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            await using (var setup = new LabDbContext(options))
            {
                await setup.Database.MigrateAsync();
                setup.Users.Add(new User { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Username = "quality", FullName = "Qualidade", PasswordHash = "x", Role = UserRole.QualityManager });
                setup.Products.Add(new Product { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Code = "P1", Name = "Produto", CommercialUnit = "kg" });
                setup.Lots.Add(new Lot { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Number = "L1", ProductId = Guid.Parse("22222222-2222-2222-2222-222222222222"), ProductSpecificationId = Guid.NewGuid(), ManufactureDate = DateOnly.FromDateTime(DateTime.Today), ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1)), OpenedAt = DateTimeOffset.Now, Status = LotStatus.InAnalysis });
                await setup.SaveChangesAsync();
            }
            await using (var db = new LabDbContext(options))
            {
                var lot = await db.Lots.Include(x => x.Releases).SingleAsync(); var user = await db.Users.SingleAsync();
                var release = LotWorkflow.Transition(lot, LotStatus.AwaitingRelease, user, "", DateTimeOffset.Now);
                db.LotReleases.Add(release);
                Assert.Equal(EntityState.Added, db.Entry(release).State);
                await db.SaveChangesAsync();
            }
            await using (var verify = new LabDbContext(options)) { Assert.Equal(LotStatus.AwaitingRelease, (await verify.Lots.SingleAsync()).Status); Assert.Single(await verify.LotReleases.ToListAsync()); }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact] public async Task ResultsCanBeCorrectedWhileOpenButNotAfterLotIsClosed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"labqc_results_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LabDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        try
        {
            await using var db = new LabDbContext(options); await db.Database.MigrateAsync();
            var user = new User { Username = "analista", FullName = "Analista", PasswordHash = "x", Role = UserRole.Analyst };
            var product = new Product { Code = "P2", Name = "Produto", CommercialUnit = "kg" };
            var lot = new Lot { Number = "L2", Product = product, ProductSpecificationId = Guid.NewGuid(), ManufactureDate = DateOnly.FromDateTime(DateTime.Today), ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1)), OpenedAt = DateTimeOffset.Now, Status = LotStatus.InAnalysis };
            var parameter = new LotParameter { Lot = lot, SourceParameterId = Guid.NewGuid(), ParameterCode = "UMI", ParameterName = "Umidade", ResultType = ResultType.Numeric, ConsolidationMethod = ConsolidationMethod.Average };
            var sample = new Sample { Lot = lot, Code = "01", CollectedAt = DateTimeOffset.Now };
            db.AddRange(user, product, lot, parameter, sample); await db.SaveChangesAsync();
            var service = new AnalysisEntryService(db);
            await service.SaveCorrectionSafeAsync(sample.Id, parameter.Id, 8.1m, null, null, user, null);
            await service.SaveCorrectionSafeAsync(sample.Id, parameter.Id, 8.2m, null, null, user, "Correção de digitação");
            Assert.Equal(2, await db.AnalysisResults.CountAsync()); Assert.Equal(8.2m, (await db.AnalysisResults.SingleAsync(x => x.IsCurrent)).NumericValue);
            lot.Status = LotStatus.Closed; await db.SaveChangesAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveCorrectionSafeAsync(sample.Id, parameter.Id, 8.3m, null, null, user, "Nova correção"));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}

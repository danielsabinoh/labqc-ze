using LabQC.Domain;
using LabQC.Reports;

namespace LabQC.Tests;

public sealed class PdfPreviewTests
{
    [Fact]
    public void GenerateReferencePreviewWhenRequested()
    {
        var output = Environment.GetEnvironmentVariable("LABQC_PDF_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        var certificate = new Certificate
        {
            Number = "2026-000285", Version = 1, IssuedAt = new DateTimeOffset(2026, 7, 22, 9, 0, 0, TimeSpan.FromHours(-3)),
            ProductName = "FARINHA BRANCA CLASSE FINA TIPO 1 — sacas 50 kg", LotNumber = "285",
            ClientName = "BOM GOSTO 2010 COMERCIO DE ALIMENTOS LTDA", City = "MAGE", State = "RJ", InvoiceNumber = "48102",
            CertifiedQuantity = 800, QuantityUnit = "sacas 50 kg", ManufactureDate = new DateOnly(2026, 7, 20), ExpiryDate = new DateOnly(2027, 7, 20)
        };
        Add(certificate, "Aspecto", ParameterCategory.Organoleptic, "Esfarelada com grânulos gelatinizados", "", "Esfarelada com grânulos gelatinizados", 1);
        Add(certificate, "Cor", ParameterCategory.Organoleptic, "Branca, com padrões Pré estabelecidos", "", "Branca, com padrões Pré estabelecidos", 2);
        Add(certificate, "Odor", ParameterCategory.Organoleptic, "Característico", "", "Característico", 3);
        Add(certificate, "Sabor", ParameterCategory.Organoleptic, "Peculiar", "", "Peculiar", 4);
        Add(certificate, "Umidade", ParameterCategory.Physicochemical, "6,9", "%", "Máx. 13,00%", 5);
        Add(certificate, "P. 10 (2,00mm)", ParameterCategory.Granulometry, "0,06", "%", "Vazar 100,00%", 6);
        Add(certificate, "P. 18 (1,00mm)", ParameterCategory.Granulometry, "12,36", "%", "Reter Máx.20,00%", 7);
        Add(certificate, "Fundo P. 200 (0,075mm)", ParameterCategory.Granulometry, "1,00", "%", "Vazar máx.3,00%", 8);
        Add(certificate, "Acidez", ParameterCategory.Physicochemical, "1,5", "mL NaOH 0,1N", "Máx.3,00mL NaOH 0,1N/10g", 9);
        Add(certificate, "Cinzas", ParameterCategory.Physicochemical, "0,7", "", "Máx. 1,4 %", 10);
        Add(certificate, "Amido", ParameterCategory.Physicochemical, "88,20", "", "Mín. 86,0 %", 11);
        var generated = new CertificatePdfService().Generate(certificate, "J. C. Oliveira & Filhos Ltda.", output);
        File.Move(generated, Path.Combine(output, "Laudo-modelo-teste.pdf"), true);
    }

    private static void Add(Certificate certificate, string name, ParameterCategory category, string result, string unit, string specification, int order) =>
        certificate.Results.Add(new CertificateResult { ParameterName = name, Category = category, Result = result, Unit = unit, Specification = specification, SortOrder = order, Conformity = ConformityStatus.Conforming });
}

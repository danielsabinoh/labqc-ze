using System.Security.Cryptography;
using LabQC.Domain;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace LabQC.Reports;

public sealed class CertificatePdfService
{
    public string Generate(Certificate certificate, string companyName, string outputDirectory)
    {
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        Directory.CreateDirectory(outputDirectory);
        var document = new Document();
        document.Info.Title = $"Certificado {certificate.Number} v{certificate.Version}";
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.4);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.4);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.5);
        var title = section.AddParagraph(companyName);
        title.Format.Font.Size = 14; title.Format.Font.Bold = true; title.Format.Alignment = ParagraphAlignment.Center;
        var heading = section.AddParagraph("CERTIFICADO DE ANÁLISES");
        heading.Format.Font.Size = 16; heading.Format.Font.Bold = true; heading.Format.Alignment = ParagraphAlignment.Center; heading.Format.SpaceAfter = Unit.FromCentimeter(.5);
        AddFieldTable(section, certificate);
        foreach (var category in certificate.Results.OrderBy(x => x.SortOrder).GroupBy(x => x.Category))
        {
            var h = section.AddParagraph(CategoryName(category.Key));
            h.Format.Font.Bold = true; h.Format.SpaceBefore = Unit.FromCentimeter(.4); h.Format.SpaceAfter = Unit.FromCentimeter(.15);
            var table = section.AddTable(); table.Borders.Width = .5;
            table.AddColumn(Unit.FromCentimeter(6)); table.AddColumn(Unit.FromCentimeter(4)); table.AddColumn(Unit.FromCentimeter(5.5));
            var header = table.AddRow(); header.Shading.Color = Colors.LightGray;
            Set(header.Cells[0], "Parâmetro", true); Set(header.Cells[1], "Resultado", true); Set(header.Cells[2], "Especificação", true);
            foreach (var result in category)
            {
                var row = table.AddRow();
                Set(row.Cells[0], result.ParameterName); Set(row.Cells[1], string.Join(" ", new[] { result.Result, result.Unit }.Where(x => !string.IsNullOrWhiteSpace(x)))); Set(row.Cells[2], result.Specification);
            }
        }
        var footer = section.Footers.Primary.AddParagraph($"Emitido em {certificate.IssuedAt:dd/MM/yyyy HH:mm} • Certificado {certificate.Number} • Versão {certificate.Version}");
        footer.Format.Font.Size = 8; footer.Format.Alignment = ParagraphAlignment.Center;
        var path = Path.Combine(outputDirectory, $"Certificado_{Sanitize(certificate.Number)}_v{certificate.Version}.pdf");
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument(); renderer.PdfDocument.Save(path);
        certificate.PdfPath = path;
        certificate.PdfSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        return path;
    }

    private static void AddFieldTable(Section s, Certificate c)
    {
        var t = s.AddTable(); t.Borders.Width = .25; t.AddColumn(Unit.FromCentimeter(4)); t.AddColumn(Unit.FromCentimeter(11.5));
        Add(t, "Produto", c.ProductName); Add(t, "Lote", c.LotNumber); Add(t, "Cliente", c.ClientName); Add(t, "Cidade/UF", $"{c.City}/{c.State}"); Add(t, "Nota fiscal", c.InvoiceNumber);
        Add(t, "Quantidade", $"{c.CertifiedQuantity:N2} {c.QuantityUnit}"); Add(t, "Fabricação / Validade", $"{c.ManufactureDate:dd/MM/yyyy} / {c.ExpiryDate:dd/MM/yyyy}");
    }
    private static void Add(Table t, string label, string value) { var r = t.AddRow(); Set(r.Cells[0], label, true); Set(r.Cells[1], value); }
    private static void Set(Cell c, string value, bool bold = false) { var p = c.AddParagraph(value); p.Format.Font.Bold = bold; p.Format.Font.Size = 9; c.VerticalAlignment = VerticalAlignment.Center; }
    private static string CategoryName(ParameterCategory c) => c switch { ParameterCategory.Physicochemical => "FÍSICO-QUÍMICO", ParameterCategory.Organoleptic => "CARACTERÍSTICAS ORGANOLÉPTICAS", ParameterCategory.Granulometry => "GRANULOMETRIA", _ => "OUTROS" };
    private static string Sanitize(string value) => string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}

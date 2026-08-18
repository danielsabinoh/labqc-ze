using System.Globalization;
using System.Security.Cryptography;
using LabQC.Domain;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace LabQC.Reports;

public sealed class CertificatePdfService
{
    private const double PageWidth = 595.28;
    private const double Left = 43;
    private const double Right = 552;
    private const double LineHeight = 13.2;
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public string Generate(Certificate certificate, string companyName, string outputDirectory)
    {
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        Directory.CreateDirectory(outputDirectory);

        using var document = new PdfDocument();
        document.Info.Title = $"Certificado {certificate.Number} v{certificate.Version}";
        document.Info.Author = "J. C. Oliveira & Filhos Ltda.";
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        using var gfx = XGraphics.FromPdfPage(page);

        var arial12 = new XFont("Arial", 12, XFontStyleEx.Regular);
        var arial10 = new XFont("Arial", 10, XFontStyleEx.Regular);
        var arial10Link = new XFont("Arial", 10, XFontStyleEx.Underline);
        var courier12 = new XFont("Courier New", 12, XFontStyleEx.Regular);

        DrawHeader(gfx, arial12, arial10, arial10Link);
        DrawCentered(gfx, "CERTIFICADO DE ANÁLISES", courier12, 134);

        var y = 168d;
        DrawAt(gfx, $"EMISSÃO.....: {certificate.IssuedAt:dd/MM/yyyy}", courier12, Left, y);
        DrawAt(gfx, $"NF Nº.....: {certificate.InvoiceNumber}", courier12, 405, y);
        y += 17;
        DrawRule(gfx, courier12, y); y += 15;

        var productName = ProductWithoutUnit(certificate.ProductName);
        DrawAt(gfx, $"CLIENTE....: {certificate.ClientName}", courier12, Left, y);
        y += LineHeight;
        DrawAt(gfx, $"CIDADE.....: {certificate.City}", courier12, Left, y);
        DrawAt(gfx, $"UF.......: {certificate.State}", courier12, 405, y);
        y += LineHeight;
        DrawAt(gfx, $"PRODUTO....: {productName}", courier12, Left, y);
        DrawAt(gfx, $"UN.MEDIDA: {certificate.QuantityUnit}", courier12, 405, y);
        y += LineHeight;
        DrawAt(gfx, $"LOTE.......: {certificate.LotNumber}", courier12, Left, y);
        DrawAt(gfx, $"QUANT....: {FormatQuantity(certificate.CertifiedQuantity)} {QuantityUnitForAmount(certificate.QuantityUnit)}", courier12, 405, y);
        y += LineHeight;
        DrawAt(gfx, $"DATA FAB...: {certificate.ManufactureDate:dd/MM/yyyy}", courier12, Left, y);
        DrawAt(gfx, $"DATA VAL.: {certificate.ExpiryDate:dd/MM/yyyy}", courier12, 405, y);
        y += 16;
        DrawRule(gfx, courier12, y); y += 22;

        var organoleptic = certificate.Results.Where(x => x.Category == ParameterCategory.Organoleptic).OrderBy(x => x.SortOrder).ToList();
        if (organoleptic.Count > 0)
            y = DrawOrganoleptic(gfx, courier12, organoleptic, y);

        var physical = certificate.Results.Where(x => x.Category != ParameterCategory.Organoleptic).OrderBy(x => x.SortOrder).ToList();
        if (physical.Count > 0)
            y = DrawPhysicalChemical(gfx, courier12, physical, y);

        DrawSignature(gfx, Math.Max(675, y + 18));

        var path = Path.Combine(outputDirectory, $"Certificado_{Sanitize(certificate.Number)}_v{certificate.Version}.pdf");
        document.Save(path);
        certificate.PdfPath = path;
        certificate.PdfSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        return path;
    }

    private static void DrawHeader(XGraphics gfx, XFont nameFont, XFont infoFont, XFont linkFont)
    {
        var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
        var logoPath = Path.Combine(assets, "logo-laudo.jpg");
        if (File.Exists(logoPath))
        {
            using var logo = XImage.FromFile(logoPath);
            gfx.DrawImage(logo, new XRect(44, 22, 67, 47), new XRect(45, 145, 900, 580), XGraphicsUnit.Point);
        }
        DrawAt(gfx, "J. C. Oliveira & Filhos Ltda.", nameFont, 116, 35);
        DrawAt(gfx, "CNPJ: 78.704.90/0001-30", infoFont, Left, 73);
        DrawAt(gfx, "INSC. ESTADUAL:83303097-34", infoFont, 328, 73);
        DrawAt(gfx, "ENDEREÇO:Estrada Jequitibá Lt 592 Dist. São Lourenço", infoFont, Left, 87);
        DrawAt(gfx, "CEP: 87.200-970", infoFont, 328, 87);
        DrawAt(gfx, "CIDADE: Cianorte", infoFont, Left, 101);
        DrawAt(gfx, "ESTADO: Paraná", infoFont, 328, 101);
        DrawAt(gfx, "FONE/FAX: (44) 3629-3215", infoFont, Left, 115);
        DrawAt(gfx, "EMAIL:", infoFont, 328, 115);
        gfx.DrawString("qualidade@alimentosdoze.com.br", linkFont, XBrushes.Blue, new XRect(365, 115, 190, 14), XStringFormats.TopLeft);
    }

    private static double DrawOrganoleptic(XGraphics gfx, XFont font, IReadOnlyList<CertificateResult> results, double y)
    {
        DrawCentered(gfx, "Características Organolépticas", font, y); y += 17;
        DrawRule(gfx, font, y); y += 14;
        DrawAt(gfx, "PARÂMETRO", font, 57, y); DrawAt(gfx, "RESULTADOS", font, 208, y); DrawAt(gfx, "ESPECIFICAÇÃO", font, 414, y); y += 14;
        DrawRule(gfx, font, y); y += 13;
        foreach (var result in results)
        {
            var resultLines = Wrap(result.Result, 28); var specificationLines = Wrap(result.Specification, 28);
            var lines = Math.Max(1, Math.Max(resultLines.Count, specificationLines.Count));
            DrawAt(gfx, result.ParameterName, font, 57, y);
            for (var i = 0; i < lines; i++)
            {
                if (i < resultLines.Count) DrawAt(gfx, resultLines[i], font, 172, y + i * LineHeight);
                if (i < specificationLines.Count) DrawAt(gfx, specificationLines[i], font, 395, y + i * LineHeight);
            }
            y += lines * LineHeight;
        }
        y += 2; DrawRule(gfx, font, y); return y + 24;
    }

    private static double DrawPhysicalChemical(XGraphics gfx, XFont font, IReadOnlyList<CertificateResult> results, double y)
    {
        DrawCentered(gfx, "Físico Químico", font, y); y += 24;
        DrawRule(gfx, font, y); y += 14;
        DrawAt(gfx, "PARÂMETRO", font, 57, y); DrawAt(gfx, "RESULTADOS", font, 286, y); DrawAt(gfx, "ESPECIFICAÇÃO", font, 390, y); y += 14;
        DrawRule(gfx, font, y); y += 13;
        foreach (var result in results)
        {
            var parameter = result.ParameterName + (string.IsNullOrWhiteSpace(result.Unit) ? "" : $", {result.Unit}");
            DrawAt(gfx, Dotted(parameter, 31), font, 57, y);
            DrawAt(gfx, result.Result, font, 318, y);
            DrawAt(gfx, result.Specification, font, 390, y);
            y += LineHeight;
        }
        y += 2; DrawRule(gfx, font, y); return y;
    }

    private static void DrawSignature(XGraphics gfx, double y)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "assinatura.png");
        if (!File.Exists(path)) return;
        using var signature = XImage.FromFile(path);
        gfx.DrawImage(signature, new XRect(370, Math.Min(y, 750), 170, 74), new XRect(10, 42, 520, 190), XGraphicsUnit.Point);
    }

    private static void DrawRule(XGraphics gfx, XFont font, double y) => DrawAt(gfx, new string('-', 71), font, Left, y);
    private static void DrawCentered(XGraphics gfx, string text, XFont font, double y) => gfx.DrawString(text, font, XBrushes.Black, new XRect(Left, y, Right - Left, 18), XStringFormats.TopCenter);
    private static void DrawAt(XGraphics gfx, string text, XFont font, double x, double y) => gfx.DrawString(text ?? "", font, XBrushes.Black, new XRect(x, y, Math.Max(1, PageWidth - x - 20), 18), XStringFormats.TopLeft);
    private static string Dotted(string text, int width) => text.Length >= width ? text : text + new string('.', width - text.Length);
    private static string ProductWithoutUnit(string name) => name.Split('—', 2, StringSplitOptions.TrimEntries)[0];
    private static string FormatQuantity(decimal value) => value.ToString(value == decimal.Truncate(value) ? "N0" : "N2", PtBr);
    private static string QuantityUnitForAmount(string unit)
    {
        var first = unit.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? unit;
        return first.ToLowerInvariant() switch { "saco" => "sacos", "saca" => "sacas", _ => first };
    }
    private static string Sanitize(string value) => string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private static List<string> Wrap(string? value, int maxChars)
    {
        var words = (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries); var result = new List<string>(); var line = "";
        foreach (var word in words)
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > maxChars) { result.Add(line); line = word; }
            else line = line.Length == 0 ? word : $"{line} {word}";
        }
        if (line.Length > 0) result.Add(line); if (result.Count == 0) result.Add(""); return result;
    }
}

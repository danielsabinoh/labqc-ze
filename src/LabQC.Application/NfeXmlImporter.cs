using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace LabQC.Application;

public sealed record NfeXmlItem(int Number, string Code, string Description, decimal Quantity, string Unit, decimal? UnitValue)
{
    public string Display => $"{Number:00} — {Description} — {Quantity.ToString("N4", CultureInfo.GetCultureInfo("pt-BR"))} {Unit}";
}

public sealed record NfeXmlData(string InvoiceNumber, string AccessKey, string ClientName, string City, string State, IReadOnlyList<NfeXmlItem> Items);

public static class NfeXmlImporter
{
    private const long MaximumCharacters = 10_000_000;

    public static NfeXmlData Parse(Stream stream)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumCharacters,
            IgnoreComments = true,
            IgnoreWhitespace = true
        };
        using var reader = XmlReader.Create(stream, settings);
        var document = XDocument.Load(reader, LoadOptions.None);
        return ParseDocument(document);
    }

    public static NfeXmlData Parse(string xml)
    {
        using var text = new StringReader(xml);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumCharacters };
        using var reader = XmlReader.Create(text, settings);
        return ParseDocument(XDocument.Load(reader, LoadOptions.None));
    }

    private static NfeXmlData ParseDocument(XDocument document)
    {
        var info = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "infNFe")
            ?? throw new InvalidDataException("O arquivo não contém uma NF-e válida (grupo infNFe ausente).");
        var ide = Child(info, "ide") ?? throw new InvalidDataException("O XML não contém os dados de identificação da NF-e.");
        var destination = Child(info, "dest") ?? throw new InvalidDataException("O XML não contém os dados do destinatário.");
        var address = Child(destination, "enderDest");

        var invoice = Value(ide, "nNF");
        var client = Value(destination, "xNome");
        if (string.IsNullOrWhiteSpace(invoice) || string.IsNullOrWhiteSpace(client))
            throw new InvalidDataException("Número da nota ou nome do destinatário não foi encontrado no XML.");

        var items = info.Elements().Where(x => x.Name.LocalName == "det").Select((detail, index) =>
        {
            var product = Child(detail, "prod") ?? throw new InvalidDataException($"O item {index + 1} não contém o grupo de produto.");
            var quantityText = Value(product, "qCom");
            if (!decimal.TryParse(quantityText, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity))
                throw new InvalidDataException($"Quantidade comercial inválida no item {index + 1}.");
            decimal? unitValue = decimal.TryParse(Value(product, "vUnCom"), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue) ? parsedValue : null;
            var number = int.TryParse(detail.Attribute("nItem")?.Value, out var parsedNumber) ? parsedNumber : index + 1;
            return new NfeXmlItem(number, Value(product, "cProd"), Value(product, "xProd"), quantity, Value(product, "uCom"), unitValue);
        }).ToList();
        if (items.Count == 0) throw new InvalidDataException("O XML da NF-e não possui itens de produto.");

        var id = info.Attribute("Id")?.Value ?? "";
        var accessKey = id.StartsWith("NFe", StringComparison.OrdinalIgnoreCase) ? id[3..] : id;
        return new NfeXmlData(invoice.Trim(), accessKey, client.Trim(), Value(address, "xMun").Trim(), Value(address, "UF").Trim().ToUpperInvariant(), items);
    }

    private static XElement? Child(XElement? parent, string localName) => parent?.Elements().FirstOrDefault(x => x.Name.LocalName == localName);
    private static string Value(XElement? parent, string localName) => Child(parent, localName)?.Value ?? "";
}

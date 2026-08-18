using System.Globalization;
using System.Security.Cryptography;
using LabQC.Domain;

namespace LabQC.Application;

public static class BrazilianDecimal
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");
    public static bool TryParse(string? text, out decimal value) => decimal.TryParse(text, NumberStyles.Number, Culture, out value);
    public static string Format(decimal value, int places) => value.ToString($"N{places}", Culture);
}

public static class PortugueseLabels
{
    public static string Category(ParameterCategory value) => value switch { ParameterCategory.Physicochemical => "Físico-químico", ParameterCategory.Organoleptic => "Organoléptico", ParameterCategory.Granulometry => "Granulometria", _ => "Outros" };
    public static string ResultType(ResultType value) => value switch { LabQC.Domain.ResultType.Numeric => "Numérico", LabQC.Domain.ResultType.Text => "Texto", LabQC.Domain.ResultType.Conformity => "Conforme / Não conforme", _ => "Seleção de opções" };
    public static string Consolidation(ConsolidationMethod value) => value switch { ConsolidationMethod.Average => "Média", ConsolidationMethod.Minimum => "Menor resultado", ConsolidationMethod.Maximum => "Maior resultado", ConsolidationMethod.Latest => "Último resultado", ConsolidationMethod.Manual => "Resultado manual", ConsolidationMethod.StandardText => "Texto padrão", _ => "Conforme / Não conforme" };
    public static string LotStatus(LotStatus value) => value switch { LabQC.Domain.LotStatus.InAnalysis => "Em análise", LabQC.Domain.LotStatus.AwaitingRelease => "Aguardando liberação", LabQC.Domain.LotStatus.Approved => "Aprovado", LabQC.Domain.LotStatus.Rejected => "Reprovado", LabQC.Domain.LotStatus.Blocked => "Bloqueado", _ => "Encerrado" };
    public static string UserRole(UserRole value) => value switch { LabQC.Domain.UserRole.Analyst => "Analista", LabQC.Domain.UserRole.QualityManager => "Responsável da Qualidade", _ => "Administrador" };
    public static string Active(bool value) => value ? "Ativo" : "Excluído / inativo";
}

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256$210000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string encoded)
    {
        var parts = encoded.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out var rounds)) return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, rounds, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public sealed record ConsolidatedResult(Guid LotParameterId, decimal? NumericValue, string? TextValue, ConformityStatus Conformity);

public static class ConsolidationEngine
{
    public static ConsolidatedResult Consolidate(LotParameter parameter, IEnumerable<AnalysisResult> source)
    {
        var results = source.Where(x => x.IsCurrent && x.IsValid).OrderBy(x => x.EnteredAt).ToList();
        decimal? numeric = null;
        string? text = null;
        switch (parameter.ConsolidationMethod)
        {
            case ConsolidationMethod.Average:
                var values = results.Where(x => x.NumericValue.HasValue).Select(x => x.NumericValue!.Value).ToList();
                numeric = values.Count == 0 ? null : values.Sum() / values.Count;
                break;
            case ConsolidationMethod.Minimum: numeric = results.Where(x => x.NumericValue.HasValue).Min(x => x.NumericValue); break;
            case ConsolidationMethod.Maximum: numeric = results.Where(x => x.NumericValue.HasValue).Max(x => x.NumericValue); break;
            case ConsolidationMethod.Latest:
                var latest = results.LastOrDefault(); numeric = latest?.NumericValue; text = latest?.TextValue; break;
            case ConsolidationMethod.Manual: numeric = parameter.ManualNumericValue; text = parameter.ManualTextValue; break;
            case ConsolidationMethod.StandardText: text = parameter.ManualTextValue ?? parameter.StandardText; break;
            case ConsolidationMethod.Conformity:
                text = results.Count > 0 && results.All(x => x.ConformityValue == true) ? "Conforme" : "Não conforme"; break;
        }
        var conformity = Evaluate(parameter, numeric, text, results);
        return new(parameter.Id, numeric, text, conformity);
    }

    private static ConformityStatus Evaluate(LotParameter p, decimal? value, string? text, List<AnalysisResult> results)
    {
        if (p.ResultType == ResultType.Numeric)
        {
            if (!value.HasValue) return ConformityStatus.Pending;
            return (p.Minimum.HasValue && value < p.Minimum) || (p.Maximum.HasValue && value > p.Maximum)
                ? ConformityStatus.NonConforming : ConformityStatus.Conforming;
        }
        if (p.ResultType == ResultType.Conformity)
            return results.Count == 0 ? ConformityStatus.Pending : results.All(x => x.ConformityValue == true) ? ConformityStatus.Conforming : ConformityStatus.NonConforming;
        if (!string.IsNullOrWhiteSpace(p.SpecificationText) && !string.IsNullOrWhiteSpace(text))
            return string.Equals(p.SpecificationText.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase) ? ConformityStatus.Conforming : ConformityStatus.NonConforming;
        return string.IsNullOrWhiteSpace(text) ? ConformityStatus.Pending : ConformityStatus.NotApplicable;
    }
}

public static class LotFactory
{
    public static Lot Create(string number, Product product, ProductSpecification spec, DateOnly manufactureDate, decimal quantity, string origin, DateTimeOffset now)
    {
        if (!spec.IsActive || spec.ProductId != product.Id) throw new InvalidOperationException("A especificação ativa não pertence ao produto.");
        var lot = new Lot { Number = number.Trim(), ProductId = product.Id, Product = product, ProductSpecificationId = spec.Id, Origin = origin, ManufactureDate = manufactureDate, ExpiryDate = manufactureDate.AddMonths(product.ShelfLifeMonths), QuantityProduced = quantity, Unit = product.CommercialUnit, OpenedAt = now };
        lot.Parameters = spec.Parameters.OrderBy(x => x.SortOrder).Select(x => new LotParameter { LotId = lot.Id, SourceParameterId = x.AnalysisParameterId, ParameterCode = x.AnalysisParameter.Code, ParameterName = x.AnalysisParameter.Name, Category = x.AnalysisParameter.Category, Unit = x.AnalysisParameter.Unit, ResultType = x.AnalysisParameter.ResultType, DecimalPlaces = x.AnalysisParameter.DecimalPlaces, Minimum = x.Minimum, Maximum = x.Maximum, SpecificationText = x.SpecificationText, StandardText = x.StandardText, ConsolidationMethod = x.ConsolidationMethod, SortOrder = x.SortOrder }).ToList();
        return lot;
    }
}

public static class LotWorkflow
{
    public static LotRelease Transition(Lot lot, LotStatus target, User user, string justification, DateTimeOffset now)
    {
        if (user.Role == UserRole.Analyst) throw new UnauthorizedAccessException("O perfil Analista não pode liberar lotes.");
        var allowed = lot.Status switch
        {
            LotStatus.InAnalysis => target == LotStatus.AwaitingRelease || target == LotStatus.Blocked,
            LotStatus.AwaitingRelease => target is LotStatus.Approved or LotStatus.Rejected or LotStatus.Blocked,
            LotStatus.Approved => target == LotStatus.Closed,
            LotStatus.Blocked => target == LotStatus.AwaitingRelease,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"Transição inválida: {lot.Status} → {target}.");
        if (target is LotStatus.Rejected or LotStatus.Blocked && string.IsNullOrWhiteSpace(justification)) throw new InvalidOperationException("Informe uma justificativa.");
        lot.Status = target;
        var release = new LotRelease { LotId = lot.Id, Decision = target, UserId = user.Id, CreatedAt = now, Justification = justification };
        lot.Releases.Add(release);
        return release;
    }
}

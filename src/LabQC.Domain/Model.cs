namespace LabQC.Domain;

public enum UserRole { Analyst, QualityManager, Administrator }
public enum ResultType { Numeric, Text, Conformity, Selection }
public enum ParameterCategory { Physicochemical, Organoleptic, Granulometry, Other }
public enum ConsolidationMethod { Average, Minimum, Maximum, Latest, Manual, StandardText, Conformity }
public enum LotStatus { InAnalysis, AwaitingRelease, Approved, Rejected, Blocked, Closed }
public enum ConformityStatus { Pending, Conforming, NonConforming, NotApplicable }
public enum CertificateStatus { Current, Superseded, Cancelled }

public abstract class Entity { public Guid Id { get; set; } = Guid.NewGuid(); }

public static class ProductFamilies
{
    public static IReadOnlyList<string> Standard { get; } = ["Farinha", "Polvilho", "Fécula", "Amido", "Outros"];

    public static string Infer(string? productName)
    {
        var name = productName ?? "";
        if (name.Contains("farinha", StringComparison.OrdinalIgnoreCase)) return "Farinha";
        if (name.Contains("polvilho", StringComparison.OrdinalIgnoreCase)) return "Polvilho";
        if (name.Contains("fécula", StringComparison.OrdinalIgnoreCase) || name.Contains("fecula", StringComparison.OrdinalIgnoreCase)) return "Fécula";
        if (name.Contains("amido", StringComparison.OrdinalIgnoreCase)) return "Amido";
        return "Outros";
    }
}

public sealed class User : Entity
{
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public DateTimeOffset? PasswordChangedAt { get; set; }
}

public sealed class Product : Entity
{
    public string Code { get; set; } = "";
    public string Family { get; set; } = "Outros";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string CommercialUnit { get; set; } = "";
    public int ShelfLifeMonths { get; set; }
    public bool IsActive { get; set; } = true;
    public string DisplayName => string.IsNullOrWhiteSpace(CommercialUnit) ? Name : $"{Name} — {CommercialUnit}";
    public List<ProductSpecification> Specifications { get; set; } = [];
}

public sealed class AnalysisParameter : Entity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public ParameterCategory Category { get; set; }
    public string Unit { get; set; } = "";
    public ResultType ResultType { get; set; }
    public int DecimalPlaces { get; set; } = 2;
    public bool IsActive { get; set; } = true;
    public List<ParameterOption> Options { get; set; } = [];
}

public sealed class ParameterOption : Entity
{
    public Guid AnalysisParameterId { get; set; }
    public AnalysisParameter AnalysisParameter { get; set; } = null!;
    public string Value { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class ProductSpecification : Entity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Version { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public bool IsActive { get; set; }
    public string ChangeReason { get; set; } = "";
    public List<ProductSpecificationParameter> Parameters { get; set; } = [];
}

public sealed class ProductSpecificationParameter : Entity
{
    public Guid ProductSpecificationId { get; set; }
    public ProductSpecification ProductSpecification { get; set; } = null!;
    public Guid AnalysisParameterId { get; set; }
    public AnalysisParameter AnalysisParameter { get; set; } = null!;
    public decimal? Minimum { get; set; }
    public decimal? Maximum { get; set; }
    public string SpecificationText { get; set; } = "";
    public string StandardText { get; set; } = "";
    public ConsolidationMethod ConsolidationMethod { get; set; }
    public int SortOrder { get; set; }
}

public sealed class Lot : Entity
{
    public string Number { get; set; } = "";
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid ProductSpecificationId { get; set; }
    public string Origin { get; set; } = "";
    public DateOnly ManufactureDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public decimal QuantityProduced { get; set; }
    public string Unit { get; set; } = "";
    public DateTimeOffset OpenedAt { get; set; }
    public string Notes { get; set; } = "";
    public LotStatus Status { get; set; } = LotStatus.InAnalysis;
    public List<LotParameter> Parameters { get; set; } = [];
    public List<Sample> Samples { get; set; } = [];
    public List<LotRelease> Releases { get; set; } = [];
}

public sealed class LotParameter : Entity
{
    public Guid LotId { get; set; }
    public Lot Lot { get; set; } = null!;
    public Guid SourceParameterId { get; set; }
    public string ParameterCode { get; set; } = "";
    public string ParameterName { get; set; } = "";
    public ParameterCategory Category { get; set; }
    public string Unit { get; set; } = "";
    public ResultType ResultType { get; set; }
    public int DecimalPlaces { get; set; }
    public decimal? Minimum { get; set; }
    public decimal? Maximum { get; set; }
    public string SpecificationText { get; set; } = "";
    public string StandardText { get; set; } = "";
    public ConsolidationMethod ConsolidationMethod { get; set; }
    public int SortOrder { get; set; }
    public decimal? ManualNumericValue { get; set; }
    public string? ManualTextValue { get; set; }
}

public sealed class Sample : Entity
{
    public Guid LotId { get; set; }
    public Lot Lot { get; set; } = null!;
    public string Code { get; set; } = "";
    public DateTimeOffset CollectedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public List<AnalysisResult> Results { get; set; } = [];
}

public sealed class AnalysisResult : Entity
{
    public Guid SampleId { get; set; }
    public Sample Sample { get; set; } = null!;
    public Guid LotParameterId { get; set; }
    public LotParameter LotParameter { get; set; } = null!;
    public decimal? NumericValue { get; set; }
    public string? TextValue { get; set; }
    public bool? ConformityValue { get; set; }
    public bool IsValid { get; set; } = true;
    public bool IsCurrent { get; set; } = true;
    public int Version { get; set; } = 1;
    public Guid? ReplacesResultId { get; set; }
    public Guid EnteredByUserId { get; set; }
    public DateTimeOffset EnteredAt { get; set; }
    public string? CorrectionReason { get; set; }
}

public sealed class LotRelease : Entity
{
    public Guid LotId { get; set; }
    public Lot Lot { get; set; } = null!;
    public LotStatus Decision { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Justification { get; set; } = "";
}

public sealed class Client : Entity
{
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class Certificate : Entity
{
    public string Number { get; set; } = "";
    public int Version { get; set; }
    public CertificateStatus Status { get; set; } = CertificateStatus.Current;
    public Guid LotId { get; set; }
    public Guid? ReplacesCertificateId { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public Guid IssuedByUserId { get; set; }
    public string ProductName { get; set; } = "";
    public string LotNumber { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";
    public decimal CertifiedQuantity { get; set; }
    public string QuantityUnit { get; set; } = "";
    public DateOnly ManufactureDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string PdfPath { get; set; } = "";
    public string PdfSha256 { get; set; } = "";
    public string? RevisionReason { get; set; }
    public List<CertificateResult> Results { get; set; } = [];
}

public sealed class CertificateResult : Entity
{
    public Guid CertificateId { get; set; }
    public Certificate Certificate { get; set; } = null!;
    public string ParameterName { get; set; } = "";
    public ParameterCategory Category { get; set; }
    public string Result { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Specification { get; set; } = "";
    public ConformityStatus Conformity { get; set; }
    public int SortOrder { get; set; }
}

public sealed class AuditEntry : Entity
{
    public Guid? UserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string EntityName { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Action { get; set; } = "";
    public string? PropertyName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Justification { get; set; }
}

public sealed class SystemSetting : Entity { public string Key { get; set; } = ""; public string Value { get; set; } = ""; }
public sealed class BackupHistory : Entity { public DateTimeOffset CreatedAt { get; set; } public Guid UserId { get; set; } public string Path { get; set; } = ""; public string Sha256 { get; set; } = ""; public bool Verified { get; set; } }

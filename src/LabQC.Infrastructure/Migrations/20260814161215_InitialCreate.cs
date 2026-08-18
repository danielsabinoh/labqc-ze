using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabQC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "labqc");

            migrationBuilder.CreateTable(
                name: "AnalysisParameters",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", nullable: false),
                    ResultType = table.Column<int>(type: "INTEGER", nullable: false),
                    DecimalPlaces = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EntityName = table.Column<string>(type: "TEXT", nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    PropertyName = table.Column<string>(type: "TEXT", nullable: true),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    Justification = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupHistory",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    Verified = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Certificates",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReplacesCertificateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IssuedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", nullable: false),
                    LotNumber = table.Column<string>(type: "TEXT", nullable: false),
                    ClientName = table.Column<string>(type: "TEXT", nullable: false),
                    City = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "TEXT", nullable: false),
                    CertifiedQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    QuantityUnit = table.Column<string>(type: "TEXT", nullable: false),
                    ManufactureDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    SnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    PdfPath = table.Column<string>(type: "TEXT", nullable: false),
                    PdfSha256 = table.Column<string>(type: "TEXT", nullable: false),
                    RevisionReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    City = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    CommercialUnit = table.Column<string>(type: "TEXT", nullable: false),
                    ShelfLifeMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParameterOptions",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnalysisParameterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParameterOptions_AnalysisParameters_AnalysisParameterId",
                        column: x => x.AnalysisParameterId,
                        principalSchema: "labqc",
                        principalTable: "AnalysisParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CertificateResults",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CertificateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParameterName = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Result = table.Column<string>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", nullable: false),
                    Specification = table.Column<string>(type: "TEXT", nullable: false),
                    Conformity = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateResults_Certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalSchema: "labqc",
                        principalTable: "Certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Lots",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductSpecificationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Origin = table.Column<string>(type: "TEXT", nullable: false),
                    ManufactureDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    QuantityProduced = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lots_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "labqc",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductSpecifications",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ChangeReason = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSpecifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSpecifications_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "labqc",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotParameters",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceParameterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParameterCode = table.Column<string>(type: "TEXT", nullable: false),
                    ParameterName = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", nullable: false),
                    ResultType = table.Column<int>(type: "INTEGER", nullable: false),
                    DecimalPlaces = table.Column<int>(type: "INTEGER", nullable: false),
                    Minimum = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    Maximum = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    SpecificationText = table.Column<string>(type: "TEXT", nullable: false),
                    StandardText = table.Column<string>(type: "TEXT", nullable: false),
                    ConsolidationMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ManualNumericValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    ManualTextValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotParameters_Lots_LotId",
                        column: x => x.LotId,
                        principalSchema: "labqc",
                        principalTable: "Lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LotReleases",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Decision = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Justification = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotReleases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotReleases_Lots_LotId",
                        column: x => x.LotId,
                        principalSchema: "labqc",
                        principalTable: "Lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Samples",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    CollectedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Samples_Lots_LotId",
                        column: x => x.LotId,
                        principalSchema: "labqc",
                        principalTable: "Lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductSpecificationParameters",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductSpecificationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnalysisParameterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Minimum = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    Maximum = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    SpecificationText = table.Column<string>(type: "TEXT", nullable: false),
                    StandardText = table.Column<string>(type: "TEXT", nullable: false),
                    ConsolidationMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSpecificationParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSpecificationParameters_AnalysisParameters_AnalysisParameterId",
                        column: x => x.AnalysisParameterId,
                        principalSchema: "labqc",
                        principalTable: "AnalysisParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductSpecificationParameters_ProductSpecifications_ProductSpecificationId",
                        column: x => x.ProductSpecificationId,
                        principalSchema: "labqc",
                        principalTable: "ProductSpecifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisResults",
                schema: "labqc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SampleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LotParameterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NumericValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    TextValue = table.Column<string>(type: "TEXT", nullable: true),
                    ConformityValue = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsValid = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    ReplacesResultId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EnteredByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnteredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CorrectionReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisResults_LotParameters_LotParameterId",
                        column: x => x.LotParameterId,
                        principalSchema: "labqc",
                        principalTable: "LotParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalysisResults_Samples_SampleId",
                        column: x => x.SampleId,
                        principalSchema: "labqc",
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisParameters_Code",
                schema: "labqc",
                table: "AnalysisParameters",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_LotParameterId",
                schema: "labqc",
                table: "AnalysisResults",
                column: "LotParameterId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_SampleId",
                schema: "labqc",
                table: "AnalysisResults",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateResults_CertificateId",
                schema: "labqc",
                table: "CertificateResults",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_Number_Version",
                schema: "labqc",
                table: "Certificates",
                columns: new[] { "Number", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotParameters_LotId",
                schema: "labqc",
                table: "LotParameters",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_LotReleases_LotId",
                schema: "labqc",
                table: "LotReleases",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_Lots_ProductId_Number",
                schema: "labqc",
                table: "Lots",
                columns: new[] { "ProductId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParameterOptions_AnalysisParameterId",
                schema: "labqc",
                table: "ParameterOptions",
                column: "AnalysisParameterId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Code",
                schema: "labqc",
                table: "Products",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecificationParameters_AnalysisParameterId",
                schema: "labqc",
                table: "ProductSpecificationParameters",
                column: "AnalysisParameterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecificationParameters_ProductSpecificationId",
                schema: "labqc",
                table: "ProductSpecificationParameters",
                column: "ProductSpecificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecifications_ProductId_Version",
                schema: "labqc",
                table: "ProductSpecifications",
                columns: new[] { "ProductId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Samples_LotId_Code",
                schema: "labqc",
                table: "Samples",
                columns: new[] { "LotId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                schema: "labqc",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                schema: "labqc",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisResults",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "AuditEntries",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "BackupHistory",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "CertificateResults",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "Clients",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "LotReleases",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "ParameterOptions",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "ProductSpecificationParameters",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "SystemSettings",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "LotParameters",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "Samples",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "Certificates",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "AnalysisParameters",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "ProductSpecifications",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "Lots",
                schema: "labqc");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "labqc");
        }
    }
}

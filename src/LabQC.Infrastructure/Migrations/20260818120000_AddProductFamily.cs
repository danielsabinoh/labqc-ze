using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabQC.Infrastructure.Migrations;

[DbContext(typeof(LabDbContext))]
[Migration("20260818120000_AddProductFamily")]
public sealed class AddProductFamily : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Family",
            schema: "labqc",
            table: "Products",
            type: "TEXT",
            nullable: false,
            defaultValue: "Outros");

        migrationBuilder.Sql("UPDATE Products SET Family = 'Farinha' WHERE lower(Name) LIKE '%farinha%';");
        migrationBuilder.Sql("UPDATE Products SET Family = 'Polvilho' WHERE lower(Name) LIKE '%polvilho%';");
        migrationBuilder.Sql("UPDATE Products SET Family = 'Fécula' WHERE lower(Name) LIKE '%fecula%' OR Name LIKE '%fécula%' OR Name LIKE '%Fécula%';");
        migrationBuilder.Sql("UPDATE Products SET Family = 'Amido' WHERE lower(Name) LIKE '%amido%';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Family", schema: "labqc", table: "Products");
    }
}

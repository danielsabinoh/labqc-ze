using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabQC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserPasswordLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                schema: "labqc",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PasswordChangedAt",
                schema: "labqc",
                table: "Users",
                type: "TEXT",
                nullable: true);

            // Existing installations could not change the seeded password before this migration.
            migrationBuilder.Sql("UPDATE Users SET MustChangePassword = 1 WHERE Username = 'admin' AND PasswordChangedAt IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                schema: "labqc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAt",
                schema: "labqc",
                table: "Users");
        }
    }
}

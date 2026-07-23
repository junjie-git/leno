using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordHashVersionColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "password_hash_version",
                table: "users",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "password_hash_version",
                table: "users");
        }
    }
}

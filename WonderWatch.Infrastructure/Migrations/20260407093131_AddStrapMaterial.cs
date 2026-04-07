using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WonderWatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStrapMaterial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StrapMaterial",
                table: "Watches",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StrapMaterial",
                table: "Watches");
        }
    }
}

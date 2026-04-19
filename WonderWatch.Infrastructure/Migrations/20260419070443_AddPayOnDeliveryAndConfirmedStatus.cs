using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WonderWatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayOnDeliveryAndConfirmedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPayOnDelivery",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPayOnDelivery",
                table: "Orders");
        }
    }
}

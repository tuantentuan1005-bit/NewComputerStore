using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewComputerStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacySpecs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Battery",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CPU",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Display",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "GPU",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Ports",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RAM",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Storage",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Battery",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CPU",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Display",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GPU",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ports",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RAM",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Storage",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Weight",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}

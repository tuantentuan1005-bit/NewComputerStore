using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewComputerStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountSystemEnterprise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DiscountEndDate",
                table: "Products",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "Products",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscountStartDate",
                table: "Products",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FlashSaleId",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Products",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "VoucherCode",
                table: "Products",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProductColors",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "OrderDetails",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalPrice",
                table: "OrderDetails",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrice",
                table: "OrderDetails",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Products_DiscountPercent",
                table: "Products",
                column: "DiscountPercent");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsDeleted",
                table: "Products",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsHidden",
                table: "Products",
                column: "IsHidden");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_FinalPrice",
                table: "OrderDetails",
                column: "FinalPrice");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_DiscountPercent",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsDeleted",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsHidden",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_OrderDetails_FinalPrice",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "DiscountEndDate",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DiscountStartDate",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FlashSaleId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "VoucherCode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProductColors");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "FinalPrice",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "OriginalPrice",
                table: "OrderDetails");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewComputerStore.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicSpecTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpecGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecGroups_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpecKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecKeys_SpecGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "SpecGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductSpecs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    SpecKeyId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSpecs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSpecs_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductSpecs_SpecKeys_SpecKeyId",
                        column: x => x.SpecKeyId,
                        principalTable: "SpecKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecs_ProductId_SpecKeyId",
                table: "ProductSpecs",
                columns: new[] { "ProductId", "SpecKeyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecs_SpecKeyId",
                table: "ProductSpecs",
                column: "SpecKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecGroups_CategoryId",
                table: "SpecGroups",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecKeys_GroupId",
                table: "SpecKeys",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductSpecs");

            migrationBuilder.DropTable(
                name: "SpecKeys");

            migrationBuilder.DropTable(
                name: "SpecGroups");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestApiDb.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPriceImagesAndPublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Entities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Entities",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    AltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.CheckConstraint("CK_ProductImages_DisplayOrder_NonNegative", "\"DisplayOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProductImages_Entities_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Entities_Price_NonNegative",
                table: "Entities",
                sql: "\"Price\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Entities_Price_NonNegative",
                table: "Entities");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Entities");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Entities");
        }
    }
}

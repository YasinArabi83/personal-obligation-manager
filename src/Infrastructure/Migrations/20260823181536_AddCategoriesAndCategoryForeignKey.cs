using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POM.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoriesAndCategoryForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_categories_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_obligations_category_id",
                table: "obligations",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_user_name",
                table: "categories",
                columns: new[] { "user_id", "name" });

            migrationBuilder.AddForeignKey(
                name: "fk_obligations_categories_category_id",
                table: "obligations",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "name", "icon", "is_default", "user_id" },
                values: new object[,]
                {
                    { Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-111111111111"), "Bills", "receipt", true, null },
                    { Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-222222222222"), "Subscriptions", "repeat", true, null },
                    { Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-333333333333"), "Documents", "file-text", true, null },
                    { Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-444444444444"), "Maintenance", "wrench", true, null },
                    { Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-555555555555"), "Appointments", "calendar", true, null },
                    { Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-666666666666"), "Personal", "user", true, null },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValues: new object[]
                {
                    Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-111111111111"),
                    Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-222222222222"),
                    Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-333333333333"),
                    Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-444444444444"),
                    Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-555555555555"),
                    Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-666666666666"),
                });

            migrationBuilder.DropForeignKey(
                name: "fk_obligations_categories_category_id",
                table: "obligations");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropIndex(
                name: "ix_obligations_category_id",
                table: "obligations");
        }
    }
}

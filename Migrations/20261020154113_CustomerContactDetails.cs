using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PusulaCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class CustomerContactDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactTitle",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Landline",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Customers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ContactTitle",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Landline",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Customers");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnafixtechInformaticaSite.Migrations
{
    /// <inheritdoc />
    public partial class FotosNoBanco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoContentType",
                table: "ServiceRequests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "PhotoData",
                table: "ServiceRequests",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoContentType",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "PhotoData",
                table: "ServiceRequests");
        }
    }
}

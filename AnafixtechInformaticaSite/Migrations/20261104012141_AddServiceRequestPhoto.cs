using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnafixtechInformaticaSite.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRequestPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "ServiceRequests",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "ServiceRequests");
        }
    }
}

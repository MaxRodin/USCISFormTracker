using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USCISFormTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfPathTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LatestPdfPath",
                table: "FormRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldPdfPath",
                table: "FormChanges",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewPdfPath",
                table: "FormChanges",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatestPdfPath",
                table: "FormRecords");

            migrationBuilder.DropColumn(
                name: "OldPdfPath",
                table: "FormChanges");

            migrationBuilder.DropColumn(
                name: "NewPdfPath",
                table: "FormChanges");
        }
    }
}

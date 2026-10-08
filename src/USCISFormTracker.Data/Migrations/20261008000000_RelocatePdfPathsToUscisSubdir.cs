using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USCISFormTracker.Data.Migrations
{
    /// <summary>
    /// PDF storage moved from the relative "pdfs/{form}/..." layout to
    /// "{PdfStorage:RootDirectory}/uscis/{form}/...", with stored paths now relative to the root.
    /// Rewrites existing "pdfs/" prefixes to "uscis/" in every path column. No files are moved.
    /// </summary>
    public partial class RelocatePdfPathsToUscisSubdir : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """UPDATE "FormRecords" SET "LatestPdfPath" = 'uscis/' || substring("LatestPdfPath" from 6) WHERE "LatestPdfPath" LIKE 'pdfs/%';""");
            migrationBuilder.Sql(
                """UPDATE "FormChanges" SET "OldPdfPath" = 'uscis/' || substring("OldPdfPath" from 6) WHERE "OldPdfPath" LIKE 'pdfs/%';""");
            migrationBuilder.Sql(
                """UPDATE "FormChanges" SET "NewPdfPath" = 'uscis/' || substring("NewPdfPath" from 6) WHERE "NewPdfPath" LIKE 'pdfs/%';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """UPDATE "FormRecords" SET "LatestPdfPath" = 'pdfs/' || substring("LatestPdfPath" from 7) WHERE "LatestPdfPath" LIKE 'uscis/%';""");
            migrationBuilder.Sql(
                """UPDATE "FormChanges" SET "OldPdfPath" = 'pdfs/' || substring("OldPdfPath" from 7) WHERE "OldPdfPath" LIKE 'uscis/%';""");
            migrationBuilder.Sql(
                """UPDATE "FormChanges" SET "NewPdfPath" = 'pdfs/' || substring("NewPdfPath" from 7) WHERE "NewPdfPath" LIKE 'uscis/%';""");
        }
    }
}

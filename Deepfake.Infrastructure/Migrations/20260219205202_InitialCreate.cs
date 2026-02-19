using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Deepfake.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeepfake = table.Column<bool>(type: "boolean", nullable: false),
                    CnnConfidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    ElaScore = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    FftAnomalyScore = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    ExifHasMetadata = table.Column<bool>(type: "boolean", nullable: false),
                    ExifCameraInfo = table.Column<string>(type: "text", nullable: true),
                    ExifSuspiciousIndicators = table.Column<string>(type: "text", nullable: true),
                    OriginalImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    GradcamImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ElaImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FftImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ThumbnailPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProcessingTimeSeconds = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisResults", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_CreatedAt",
                table: "AnalysisResults",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_Status",
                table: "AnalysisResults",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisResults");
        }
    }
}

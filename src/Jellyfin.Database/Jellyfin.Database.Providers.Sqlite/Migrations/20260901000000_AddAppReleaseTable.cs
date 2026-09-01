using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAppReleaseTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppReleases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionString = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    VersionCode = table.Column<int>(type: "INTEGER", nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false, defaultValue: "stable"),
                    ReleaseDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Changelog = table.Column<string>(type: "TEXT", nullable: true),
                    DownloadUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    Checksum = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Mandatory = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    MinVersion = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    MinServerVersion = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ReleaseNotesUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppReleases", x => x.Id);
                    table.UniqueConstraint("AK_AppReleases_Channel_VersionCode", x => new { x.Channel, x.VersionCode });
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppReleases_Channel",
                table: "AppReleases",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_AppReleases_VersionCode",
                table: "AppReleases",
                column: "VersionCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppReleases");
        }
    }
}

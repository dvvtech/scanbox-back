using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scanbox.Api.DAL.Migrations;

/// <inheritdoc />
public partial class _20261125061222_InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "devices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MacAddress = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                PairingCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                ClassName = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_devices", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "device_tag_mappings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                SubjectId = table.Column<int>(type: "integer", nullable: false),
                RfidTag = table.Column<string>(type: "character varying(62)", maxLength: 62, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_device_tag_mappings", x => x.Id);
                table.ForeignKey(
                    name: "FK_device_tag_mappings_devices_DeviceId",
                    column: x => x.DeviceId,
                    principalTable: "devices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_device_tag_mappings_DeviceId_RfidTag",
            table: "device_tag_mappings",
            columns: new[] { "DeviceId", "RfidTag" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_device_tag_mappings_DeviceId_SubjectId",
            table: "device_tag_mappings",
            columns: new[] { "DeviceId", "SubjectId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_devices_MacAddress",
            table: "devices",
            column: "MacAddress",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "device_tag_mappings");

        migrationBuilder.DropTable(
            name: "devices");
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentVerificationAPI.Migrations
{
    public partial class AddUpdatedAtToPersonalInfo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PersonalInfo",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "EmailLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 27, 15, 14, 54, 426, DateTimeKind.Utc).AddTicks(9516));

            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 27, 15, 14, 54, 426, DateTimeKind.Utc).AddTicks(9524));

            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 27, 15, 14, 54, 426, DateTimeKind.Utc).AddTicks(9528));

            migrationBuilder.UpdateData(
                table: "Forms",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 15, 14, 54, 426, DateTimeKind.Utc).AddTicks(9223));

            migrationBuilder.UpdateData(
                table: "Forms",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 15, 14, 54, 426, DateTimeKind.Utc).AddTicks(9241));

            migrationBuilder.UpdateData(
                table: "PersonalInfo",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 15, 14, 54, 426, DateTimeKind.Utc).AddTicks(9476));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PersonalInfo");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "EmailLogs");

            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 27, 2, 4, 38, 237, DateTimeKind.Utc).AddTicks(74));

            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 27, 2, 4, 38, 237, DateTimeKind.Utc).AddTicks(80));

            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 27, 2, 4, 38, 237, DateTimeKind.Utc).AddTicks(84));

            migrationBuilder.UpdateData(
                table: "Forms",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 2, 4, 38, 236, DateTimeKind.Utc).AddTicks(9547));

            migrationBuilder.UpdateData(
                table: "Forms",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 2, 4, 38, 236, DateTimeKind.Utc).AddTicks(9551));

            migrationBuilder.UpdateData(
                table: "PersonalInfo",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 2, 4, 38, 237, DateTimeKind.Utc).AddTicks(25));
        }
    }
}

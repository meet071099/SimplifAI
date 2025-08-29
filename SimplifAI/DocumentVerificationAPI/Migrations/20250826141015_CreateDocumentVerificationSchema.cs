using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentVerificationAPI.Migrations
{
    public partial class CreateDocumentVerificationSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 26, 14, 10, 14, 724, DateTimeKind.Utc).AddTicks(2518));

            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 26, 14, 10, 14, 724, DateTimeKind.Utc).AddTicks(2531));

            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 26, 14, 10, 14, 724, DateTimeKind.Utc).AddTicks(2540));

            migrationBuilder.UpdateData(
                table: "Forms",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 14, 10, 14, 724, DateTimeKind.Utc).AddTicks(2056));

            migrationBuilder.UpdateData(
                table: "Forms",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 14, 10, 14, 724, DateTimeKind.Utc).AddTicks(2067));

            migrationBuilder.UpdateData(
                table: "PersonalInfo",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 14, 10, 14, 724, DateTimeKind.Utc).AddTicks(2454));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 26, 14, 8, 42, 538, DateTimeKind.Utc).AddTicks(8315));

            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 26, 14, 8, 42, 538, DateTimeKind.Utc).AddTicks(8326));

            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 26, 14, 8, 42, 538, DateTimeKind.Utc).AddTicks(8333));

            migrationBuilder.UpdateData(
                table: "Forms",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 14, 8, 42, 538, DateTimeKind.Utc).AddTicks(7943));

            migrationBuilder.UpdateData(
                table: "Forms",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 14, 8, 42, 538, DateTimeKind.Utc).AddTicks(7951));

            migrationBuilder.UpdateData(
                table: "PersonalInfo",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 14, 8, 42, 538, DateTimeKind.Utc).AddTicks(8262));
        }
    }
}

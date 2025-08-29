using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentVerificationAPI.Migrations
{
    public partial class AddPerformanceIndexesAndLogging : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledFor",
                table: "EmailQueue",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Success",
                table: "EmailLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

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

            migrationBuilder.CreateIndex(
                name: "IX_PersonalInfo_Email",
                table: "PersonalInfo",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalInfo_FirstName_LastName",
                table: "PersonalInfo",
                columns: new[] { "FirstName", "LastName" });

            migrationBuilder.CreateIndex(
                name: "IX_Forms_CreatedAt",
                table: "Forms",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Forms_RecruiterEmail",
                table: "Forms",
                column: "RecruiterEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Forms_RecruiterEmail_Status",
                table: "Forms",
                columns: new[] { "RecruiterEmail", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Forms_Status",
                table: "Forms",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmailQueue_CreatedAt",
                table: "EmailQueue",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmailQueue_Priority",
                table: "EmailQueue",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_EmailQueue_ScheduledFor",
                table: "EmailQueue",
                column: "ScheduledFor");

            migrationBuilder.CreateIndex(
                name: "IX_EmailQueue_Status",
                table: "EmailQueue",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmailQueue_Status_Priority_CreatedAt",
                table: "EmailQueue",
                columns: new[] { "Status", "Priority", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_AttemptedAt",
                table: "EmailLogs",
                column: "AttemptedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_EmailQueueId_AttemptedAt",
                table: "EmailLogs",
                columns: new[] { "EmailQueueId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_Success",
                table: "EmailLogs",
                column: "Success");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentType",
                table: "Documents",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_FormId_DocumentType",
                table: "Documents",
                columns: new[] { "FormId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedAt",
                table: "Documents",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_VerificationStatus",
                table: "Documents",
                column: "VerificationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_VerificationStatus_UploadedAt",
                table: "Documents",
                columns: new[] { "VerificationStatus", "UploadedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PersonalInfo_Email",
                table: "PersonalInfo");

            migrationBuilder.DropIndex(
                name: "IX_PersonalInfo_FirstName_LastName",
                table: "PersonalInfo");

            migrationBuilder.DropIndex(
                name: "IX_Forms_CreatedAt",
                table: "Forms");

            migrationBuilder.DropIndex(
                name: "IX_Forms_RecruiterEmail",
                table: "Forms");

            migrationBuilder.DropIndex(
                name: "IX_Forms_RecruiterEmail_Status",
                table: "Forms");

            migrationBuilder.DropIndex(
                name: "IX_Forms_Status",
                table: "Forms");

            migrationBuilder.DropIndex(
                name: "IX_EmailQueue_CreatedAt",
                table: "EmailQueue");

            migrationBuilder.DropIndex(
                name: "IX_EmailQueue_Priority",
                table: "EmailQueue");

            migrationBuilder.DropIndex(
                name: "IX_EmailQueue_ScheduledFor",
                table: "EmailQueue");

            migrationBuilder.DropIndex(
                name: "IX_EmailQueue_Status",
                table: "EmailQueue");

            migrationBuilder.DropIndex(
                name: "IX_EmailQueue_Status_Priority_CreatedAt",
                table: "EmailQueue");

            migrationBuilder.DropIndex(
                name: "IX_EmailLogs_AttemptedAt",
                table: "EmailLogs");

            migrationBuilder.DropIndex(
                name: "IX_EmailLogs_EmailQueueId_AttemptedAt",
                table: "EmailLogs");

            migrationBuilder.DropIndex(
                name: "IX_EmailLogs_Success",
                table: "EmailLogs");

            migrationBuilder.DropIndex(
                name: "IX_Documents_DocumentType",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_FormId_DocumentType",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_UploadedAt",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_VerificationStatus",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_VerificationStatus_UploadedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ScheduledFor",
                table: "EmailQueue");

            migrationBuilder.DropColumn(
                name: "Success",
                table: "EmailLogs");

            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 26, 16, 48, 0, 799, DateTimeKind.Utc).AddTicks(4332));

            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 26, 16, 48, 0, 799, DateTimeKind.Utc).AddTicks(4345));

            migrationBuilder.UpdateData(
                table: "Documents",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"),
                column: "UploadedAt",
                value: new DateTime(2025, 8, 26, 16, 48, 0, 799, DateTimeKind.Utc).AddTicks(4355));

            migrationBuilder.UpdateData(
                table: "Forms",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 16, 48, 0, 799, DateTimeKind.Utc).AddTicks(3710));

            migrationBuilder.UpdateData(
                table: "Forms",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 16, 48, 0, 799, DateTimeKind.Utc).AddTicks(3723));

            migrationBuilder.UpdateData(
                table: "PersonalInfo",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 16, 48, 0, 799, DateTimeKind.Utc).AddTicks(4253));
        }
    }
}

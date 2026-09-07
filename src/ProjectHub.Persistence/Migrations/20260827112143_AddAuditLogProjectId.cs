using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectHub.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogProjectId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                schema: "auditing",
                table: "audit_logs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_ProjectId",
                schema: "auditing",
                table: "audit_logs",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_logs_ProjectId",
                schema: "auditing",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "auditing",
                table: "audit_logs");
        }
    }
}

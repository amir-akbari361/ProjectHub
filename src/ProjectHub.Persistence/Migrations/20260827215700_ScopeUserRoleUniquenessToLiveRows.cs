using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectHub.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Narrows the (UserId, RoleId) unique index to live rows — "WHERE [IsDeleted] = 0".
    ///
    /// Revoking a global role SOFT-deletes the join row, but the old index counted that tombstone, so granting
    /// a role the account had ever held before violated uniqueness and the request failed. The constraint now
    /// says what the application means: at most one ACTIVE assignment per user/role pair.
    ///
    /// NOTHING IS DELETED. This replaces an index definition only — no column is dropped and no row is
    /// touched, so it holds to the additive-only rule for this database. The index is recreated over the same
    /// two columns in the same statement pair, and Down() restores the unfiltered form exactly.
    /// </remarks>
    public partial class ScopeUserRoleUniquenessToLiveRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_roles_UserId_RoleId",
                schema: "identity",
                table: "user_roles");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_UserId_RoleId",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "UserId", "RoleId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_roles_UserId_RoleId",
                schema: "identity",
                table: "user_roles");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_UserId_RoleId",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "UserId", "RoleId" },
                unique: true);
        }
    }
}

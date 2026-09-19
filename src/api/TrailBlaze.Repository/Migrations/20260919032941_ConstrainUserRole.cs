using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrailBlaze.Repository.Migrations
{
    /// <summary>
    /// Makes <c>users.Role</c> required, defaulted to <c>User</c>, and limited to the two values
    /// the PRD defines.
    /// </summary>
    /// <remarks>
    /// <b>The three steps are ordered, and the scaffolded migration had only two of them.</b>
    /// The <c>UPDATE</c> must run first, because SQL Server refuses <c>ALTER COLUMN … NOT NULL</c>
    /// while any row holds <c>NULL</c> and EF cannot know what a null should have meant. Both
    /// halves of its <c>WHERE</c> matter: the second covers a value outside the closed set, which
    /// the constraint would otherwise refuse and stop the deploy on a row nobody was thinking
    /// about. <c>'Admin'</c> is excluded from the rewrite so the one value that grants something
    /// survives. The constraint goes last, after the data can satisfy it.
    /// <para>
    /// <c>Down</c> does not restore the roles the backfill replaced — a rewritten value leaves
    /// nothing to restore it from, which is the shape of every data-carrying migration here.
    /// </para>
    /// </remarks>
    public partial class ConstrainUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [Users] SET [Role] = N'User' "
                + "WHERE [Role] IS NULL OR [Role] NOT IN (N'User', N'Admin');");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "User",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Role",
                table: "Users",
                sql: "[Role] IN ('User', 'Admin')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Role",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "User");
        }
    }
}

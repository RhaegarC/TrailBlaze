using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrailBlaze.Repository.Migrations
{
    /// <summary>
    /// Makes <c>users.Role</c> required, defaulted to <c>User</c>, and limited to the two values
    /// the PRD defines.
    /// </summary>
    /// <remarks>
    /// <para><b>The <c>UPDATE</c> below is hand-written and has to run first.</b> SQL Server
    /// refuses <c>ALTER COLUMN … NOT NULL</c> while any row holds <c>NULL</c> — and every row
    /// written before this migration does, because the column had no default and nothing ever set
    /// it. The scaffolded migration contained no statement for that at all: EF can see that the
    /// column became required and cannot know what a null should have meant.</para>
    ///
    /// <para><b>Both halves of its <c>WHERE</c> are load-bearing.</b> <c>NULL</c> is the obvious
    /// one. The second covers a value outside the closed set — a hand-edited <c>'SuperUser'</c>,
    /// or one from an earlier idea of what the set was — which the check constraint added below
    /// would otherwise refuse, stopping the deploy on a row nobody was thinking about. Both land
    /// on <c>User</c>, and <c>User</c> is the safe direction: an unrecognised role loses whatever
    /// it had rather than keeping it, and <c>'Admin'</c> is deliberately excluded from the rewrite
    /// so the one value that grants something survives untouched.</para>
    ///
    /// <para><b>The constraint is added last, after the data can satisfy it.</b> Added before the
    /// backfill it would be rejected by the very rows it exists to describe.</para>
    ///
    /// <para><b>What this cannot undo.</b> The <c>Down</c> restores the column's nullability and
    /// drops the constraint; it does not restore the roles the backfill replaced, because a
    /// rewritten value leaves nothing behind to restore it from. That is the shape of every
    /// data-carrying migration here and is stated rather than implied.</para>
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

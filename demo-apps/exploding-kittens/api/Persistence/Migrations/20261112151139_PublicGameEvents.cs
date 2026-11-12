using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExplodingKittens.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublicGameEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecentEventsJson",
                schema: "exploding_kittens",
                table: "GameStates",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                schema: "exploding_kittens",
                table: "GameStates",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecentEventsJson",
                schema: "exploding_kittens",
                table: "GameStates");

            migrationBuilder.DropColumn(
                name: "Revision",
                schema: "exploding_kittens",
                table: "GameStates");
        }
    }
}

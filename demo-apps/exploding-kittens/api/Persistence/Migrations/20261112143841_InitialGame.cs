using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExplodingKittens.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "exploding_kittens");

            migrationBuilder.CreateTable(
                name: "Rooms",
                schema: "exploding_kittens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteCode = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameStates",
                schema: "exploding_kittens",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnOrderJson = table.Column<string>(type: "text", nullable: false),
                    DeckJson = table.Column<string>(type: "text", nullable: false),
                    DiscardPileJson = table.Column<string>(type: "text", nullable: false),
                    PlayerHandsJson = table.Column<string>(type: "text", nullable: false),
                    EliminatedUserIdsJson = table.Column<string>(type: "text", nullable: false),
                    CurrentTurnIndex = table.Column<int>(type: "integer", nullable: false),
                    PendingDraws = table.Column<int>(type: "integer", nullable: false),
                    WinnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LastActionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameStates", x => x.RoomId);
                    table.ForeignKey(
                        name: "FK_GameStates_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "exploding_kittens",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomPlayers",
                schema: "exploding_kittens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SeatIndex = table.Column<int>(type: "integer", nullable: false),
                    IsHost = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsEliminated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomPlayers_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "exploding_kittens",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomPlayers_RoomId_UserId",
                schema: "exploding_kittens",
                table: "RoomPlayers",
                columns: new[] { "RoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_InviteCode",
                schema: "exploding_kittens",
                table: "Rooms",
                column: "InviteCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameStates",
                schema: "exploding_kittens");

            migrationBuilder.DropTable(
                name: "RoomPlayers",
                schema: "exploding_kittens");

            migrationBuilder.DropTable(
                name: "Rooms",
                schema: "exploding_kittens");
        }
    }
}

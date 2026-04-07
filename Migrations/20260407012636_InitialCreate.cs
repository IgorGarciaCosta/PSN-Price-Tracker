using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PsnPriceTracker.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alertas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TelegramChatId = table.Column<long>(type: "INTEGER", nullable: false),
                    NomeDoJogo = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    UrlDoJogo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PrecoAlvo = table.Column<decimal>(type: "TEXT", nullable: false),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NotificadoEm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alertas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChaveHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TelegramChatId = table.Column<long>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alertas_Ativo_TelegramChatId",
                table: "Alertas",
                columns: new[] { "Ativo", "TelegramChatId" });

            migrationBuilder.CreateIndex(
                name: "IX_Alertas_TelegramChatId_UrlDoJogo_Ativo",
                table: "Alertas",
                columns: new[] { "TelegramChatId", "UrlDoJogo", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_ChaveHash",
                table: "ApiKeys",
                column: "ChaveHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alertas");

            migrationBuilder.DropTable(
                name: "ApiKeys");
        }
    }
}

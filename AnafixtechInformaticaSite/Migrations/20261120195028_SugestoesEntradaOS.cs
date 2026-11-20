using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AnafixtechInformaticaSite.Migrations
{
    /// <inheritdoc />
    public partial class SugestoesEntradaOS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceOrderId = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DataSugerida = table.Column<DateOnly>(type: "date", nullable: false),
                    FormaPagamentoSugerida = table.Column<string>(type: "text", nullable: false),
                    SocioSugerido = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Categoria = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    MotivoCriacao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DecididaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DecididoPor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    MotivoRejeicao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FinancialEntryId = table.Column<int>(type: "integer", nullable: true),
                    Token = table.Column<Guid>(type: "uuid", nullable: false),
                    ValorCombinadoNaCriacao = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorRecebidoNaCriacao = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialSuggestions_FinancialEntries_FinancialEntryId",
                        column: x => x.FinancialEntryId,
                        principalTable: "FinancialEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinancialSuggestions_ServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialSuggestions_FinancialEntryId",
                table: "FinancialSuggestions",
                column: "FinancialEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialSuggestions_ServiceOrderId",
                table: "FinancialSuggestions",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialSuggestions_Status",
                table: "FinancialSuggestions",
                column: "Status");

            // REGRA: no máximo UMA sugestão pendente por OS.
            // É um índice PARCIAL: só vale para Status = 'Pendente'. Sugestões aprovadas,
            // ignoradas ou canceladas ficam no histórico e não ocupam esse lugar.
            // O EF não consegue expressar "WHERE" num índice, por isso o SQL direto.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_FinancialSuggestions_UmaPendentePorOS"
                    ON "FinancialSuggestions" ("ServiceOrderId")
                    WHERE "Status" = 'Pendente';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O Down só é usado se alguém desfizer a migration explicitamente.
            // O Up desta migration é 100% aditivo: cria tabela, índices e nada mais.
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_FinancialSuggestions_UmaPendentePorOS";
                """);

            migrationBuilder.DropTable(
                name: "FinancialSuggestions");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CouchIA.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sessoes",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultima_atividade_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    entrevista = table.Column<string>(type: "jsonb", nullable: false),
                    perfil = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessoes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "feedbacks",
                columns: table => new
                {
                    sessao_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    area_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    motivo = table.Column<string>(type: "text", nullable: true),
                    em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feedbacks", x => new { x.sessao_id, x.area_id });
                    table.ForeignKey(
                        name: "FK_feedbacks_sessoes_sessao_id",
                        column: x => x.sessao_id,
                        principalTable: "sessoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mensagens",
                columns: table => new
                {
                    sessao_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    client_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    papel = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mensagens", x => new { x.sessao_id, x.ordem });
                    table.ForeignKey(
                        name: "FK_mensagens_sessoes_sessao_id",
                        column: x => x.sessao_id,
                        principalTable: "sessoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "planos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sessao_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    area_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    conteudo = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planos", x => x.id);
                    table.ForeignKey(
                        name: "FK_planos_sessoes_sessao_id",
                        column: x => x.sessao_id,
                        principalTable: "sessoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recomendacoes",
                columns: table => new
                {
                    sessao_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    area_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pontuacao = table.Column<int>(type: "integer", nullable: false),
                    justificativa = table.Column<string>(type: "text", nullable: false),
                    ordem_original = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recomendacoes", x => new { x.sessao_id, x.area_id });
                    table.ForeignKey(
                        name: "FK_recomendacoes_sessoes_sessao_id",
                        column: x => x.sessao_id,
                        principalTable: "sessoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_mensagens_sessao_client_message_id",
                table: "mensagens",
                columns: new[] { "sessao_id", "client_message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_planos_sessao_id",
                table: "planos",
                column: "sessao_id");

            migrationBuilder.CreateIndex(
                name: "ix_sessoes_ultima_atividade_em",
                table: "sessoes",
                column: "ultima_atividade_em");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feedbacks");

            migrationBuilder.DropTable(
                name: "mensagens");

            migrationBuilder.DropTable(
                name: "planos");

            migrationBuilder.DropTable(
                name: "recomendacoes");

            migrationBuilder.DropTable(
                name: "sessoes");
        }
    }
}

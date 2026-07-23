using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellucSketch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoricoAlteracoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntidadeTipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntidadeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Acao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlantaId = table.Column<Guid>(type: "uuid", nullable: true),
                    DadosAnterioresJson = table.Column<string>(type: "text", nullable: true),
                    DadosNovosJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoAlteracoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plantas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjetoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NomeCliente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TipoArquivoOrigem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CaminhoArquivoOriginal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DataImportacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plantas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projetos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CriadoPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projetos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Camadas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlantaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Visivel = table.Column<bool>(type: "boolean", nullable: false),
                    Bloqueada = table.Column<bool>(type: "boolean", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    ImagemRasterCaminho = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Camadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Camadas_Plantas_PlantaId",
                        column: x => x.PlantaId,
                        principalTable: "Plantas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Camadas_PlantaId",
                table: "Camadas",
                column: "PlantaId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoAlteracoes_PlantaId",
                table: "HistoricoAlteracoes",
                column: "PlantaId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Camadas");

            migrationBuilder.DropTable(
                name: "HistoricoAlteracoes");

            migrationBuilder.DropTable(
                name: "Projetos");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Plantas");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Camdas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarEdicaoPendenteCamada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EdicoesPendentesCamada",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlantaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CamadaId = table.Column<Guid>(type: "uuid", nullable: true),
                    TipoOperacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DadosAntesJson = table.Column<string>(type: "text", nullable: true),
                    DadosDepoisJson = table.Column<string>(type: "text", nullable: false),
                    Responsavel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DataSolicitacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataResposta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MotivoRejeicao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdicoesPendentesCamada", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EdicoesPendentesCamada_PlantaId",
                table: "EdicoesPendentesCamada",
                column: "PlantaId");

            migrationBuilder.CreateIndex(
                name: "IX_EdicoesPendentesCamada_Status",
                table: "EdicoesPendentesCamada",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EdicoesPendentesCamada");
        }
    }
}

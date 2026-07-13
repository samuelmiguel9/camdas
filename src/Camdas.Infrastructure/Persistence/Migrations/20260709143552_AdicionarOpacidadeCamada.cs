using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Camdas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarOpacidadeCamada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Opacidade",
                table: "Camadas",
                type: "double precision",
                nullable: false,
                defaultValue: 1.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Opacidade",
                table: "Camadas");
        }
    }
}

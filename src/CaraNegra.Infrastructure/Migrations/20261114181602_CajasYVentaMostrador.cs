using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaraNegra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CajasYVentaMostrador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsMostrador",
                table: "Mesas",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Caja",
                table: "Jornadas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EsMostrador",
                table: "Categorias",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsMostrador",
                table: "Mesas");

            migrationBuilder.DropColumn(
                name: "Caja",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "EsMostrador",
                table: "Categorias");
        }
    }
}

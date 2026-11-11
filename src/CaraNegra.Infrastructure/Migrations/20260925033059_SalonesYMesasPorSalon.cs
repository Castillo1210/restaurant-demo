using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaraNegra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalonesYMesasPorSalon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Mesas_NumeroMesa",
                table: "Mesas");

            migrationBuilder.AddColumn<int>(
                name: "SalonId",
                table: "Mesas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Salones",
                columns: table => new
                {
                    SalonId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nombre = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Descripcion = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    EstaActivo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Salones", x => x.SalonId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Mesas_SalonId_NumeroMesa",
                table: "Mesas",
                columns: new[] { "SalonId", "NumeroMesa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Salones_Nombre",
                table: "Salones",
                column: "Nombre",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Mesas_Salones_SalonId",
                table: "Mesas",
                column: "SalonId",
                principalTable: "Salones",
                principalColumn: "SalonId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Mesas_Salones_SalonId",
                table: "Mesas");

            migrationBuilder.DropTable(
                name: "Salones");

            migrationBuilder.DropIndex(
                name: "IX_Mesas_SalonId_NumeroMesa",
                table: "Mesas");

            migrationBuilder.DropColumn(
                name: "SalonId",
                table: "Mesas");

            migrationBuilder.CreateIndex(
                name: "IX_Mesas_NumeroMesa",
                table: "Mesas",
                column: "NumeroMesa",
                unique: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaraNegra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class JornadasYCierreCaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JornadaId",
                table: "Pedidos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JornadaId",
                table: "Pagos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Jornadas",
                columns: table => new
                {
                    JornadaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FechaOperativa = table.Column<DateTime>(type: "date", nullable: false),
                    Estado = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AbiertaEn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AbiertaPorUsuarioId = table.Column<int>(type: "int", nullable: false),
                    AbiertaPor = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FondoInicial = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ObservacionApertura = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CerradaEn = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CerradaPorUsuarioId = table.Column<int>(type: "int", nullable: true),
                    CerradaPor = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ObservacionCierre = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalVentas = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CantidadPedidos = table.Column<int>(type: "int", nullable: false),
                    TotalIngresos = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TotalEgresos = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    EfectivoEsperado = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    EfectivoContado = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DiferenciaTotal = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jornadas", x => x.JornadaId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "JornadaArqueos",
                columns: table => new
                {
                    JornadaArqueoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    JornadaId = table.Column<int>(type: "int", nullable: false),
                    MetodoPagoId = table.Column<int>(type: "int", nullable: true),
                    MetodoPago = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EsEfectivo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Ventas = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Esperado = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Contado = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Diferencia = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JornadaArqueos", x => x.JornadaArqueoId);
                    table.ForeignKey(
                        name: "FK_JornadaArqueos_Jornadas_JornadaId",
                        column: x => x.JornadaId,
                        principalTable: "Jornadas",
                        principalColumn: "JornadaId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MovimientosCaja",
                columns: table => new
                {
                    MovimientoCajaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    JornadaId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Monto = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Motivo = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    RegistradoPor = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EstaAnulado = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AnuladoEn = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AnuladoPor = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreadoEn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCaja", x => x.MovimientoCajaId);
                    table.ForeignKey(
                        name: "FK_MovimientosCaja_Jornadas_JornadaId",
                        column: x => x.JornadaId,
                        principalTable: "Jornadas",
                        principalColumn: "JornadaId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_JornadaId",
                table: "Pedidos",
                column: "JornadaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_JornadaId",
                table: "Pagos",
                column: "JornadaId");

            migrationBuilder.CreateIndex(
                name: "IX_JornadaArqueos_JornadaId",
                table: "JornadaArqueos",
                column: "JornadaId");

            migrationBuilder.CreateIndex(
                name: "IX_Jornadas_Estado",
                table: "Jornadas",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Jornadas_FechaOperativa",
                table: "Jornadas",
                column: "FechaOperativa");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_JornadaId",
                table: "MovimientosCaja",
                column: "JornadaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_Jornadas_JornadaId",
                table: "Pagos",
                column: "JornadaId",
                principalTable: "Jornadas",
                principalColumn: "JornadaId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pedidos_Jornadas_JornadaId",
                table: "Pedidos",
                column: "JornadaId",
                principalTable: "Jornadas",
                principalColumn: "JornadaId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_Jornadas_JornadaId",
                table: "Pagos");

            migrationBuilder.DropForeignKey(
                name: "FK_Pedidos_Jornadas_JornadaId",
                table: "Pedidos");

            migrationBuilder.DropTable(
                name: "JornadaArqueos");

            migrationBuilder.DropTable(
                name: "MovimientosCaja");

            migrationBuilder.DropTable(
                name: "Jornadas");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_JornadaId",
                table: "Pedidos");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_JornadaId",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "JornadaId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "JornadaId",
                table: "Pagos");
        }
    }
}

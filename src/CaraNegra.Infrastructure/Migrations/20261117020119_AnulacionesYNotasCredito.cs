using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaraNegra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AnulacionesYNotasCredito : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnulacionEstado",
                table: "Comprobantes",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "AnulacionIntentos",
                table: "Comprobantes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AnulacionMensaje",
                table: "Comprobantes",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AnulacionMetodo",
                table: "Comprobantes",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AnulacionMotivo",
                table: "Comprobantes",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "AnulacionSolicitadaEn",
                table: "Comprobantes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AnulacionUltimoEnvioEn",
                table: "Comprobantes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AnuladoPorUsuarioId",
                table: "Comprobantes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotasCredito",
                columns: table => new
                {
                    NotaCreditoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ComprobanteId = table.Column<int>(type: "int", nullable: false),
                    Serie = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Correlativo = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Motivo = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Correcciones = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OpGravada = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Igv = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Estado = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ambiente = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: false, defaultValue: "SIMULADO")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HashResumen = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MensajeSunat = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    XmlUrl = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CdrUrl = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PdfUrl = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IntentosEnvio = table.Column<int>(type: "int", nullable: false),
                    UltimoEnvioEn = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasCredito", x => x.NotaCreditoId);
                    table.ForeignKey(
                        name: "FK_NotasCredito_Comprobantes_ComprobanteId",
                        column: x => x.ComprobanteId,
                        principalTable: "Comprobantes",
                        principalColumn: "ComprobanteId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCredito_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "UsuarioId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Comprobantes_AnulacionEstado",
                table: "Comprobantes",
                column: "AnulacionEstado");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_ComprobanteId",
                table: "NotasCredito",
                column: "ComprobanteId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_Estado",
                table: "NotasCredito",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_Serie_Correlativo",
                table: "NotasCredito",
                columns: new[] { "Serie", "Correlativo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_UsuarioId",
                table: "NotasCredito",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotasCredito");

            migrationBuilder.DropIndex(
                name: "IX_Comprobantes_AnulacionEstado",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "AnulacionEstado",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "AnulacionIntentos",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "AnulacionMensaje",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "AnulacionMetodo",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "AnulacionMotivo",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "AnulacionSolicitadaEn",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "AnulacionUltimoEnvioEn",
                table: "Comprobantes");

            migrationBuilder.DropColumn(
                name: "AnuladoPorUsuarioId",
                table: "Comprobantes");
        }
    }
}

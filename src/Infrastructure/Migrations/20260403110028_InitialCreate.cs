using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fund_risk_limit_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    max_order_size = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    daily_loss_limit = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    concentration_limit = table.Column<decimal>(type: "numeric(18,8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fund_risk_limit_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "funds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    manager_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fund_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fund_risk_limits",
                columns: table => new
                {
                    fund_id = table.Column<Guid>(type: "uuid", nullable: false),
                    max_order_size = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    daily_loss_limit = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    concentration_limit = table.Column<decimal>(type: "numeric(18,8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fund_risk_limits", x => x.fund_id);
                    table.ForeignKey(
                        name: "FK_fund_risk_limits_funds_fund_id",
                        column: x => x.fund_id,
                        principalTable: "funds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_fund_risk_limit_templates_name",
                table: "fund_risk_limit_templates",
                column: "template_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_funds_name",
                table: "funds",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_order_events_aggregate_id",
                table: "order_events",
                column: "aggregate_id");

            migrationBuilder.CreateIndex(
                name: "uq_order_events_aggregate_version",
                table: "order_events",
                columns: new[] { "aggregate_id", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fund_risk_limit_templates");

            migrationBuilder.DropTable(
                name: "fund_risk_limits");

            migrationBuilder.DropTable(
                name: "order_events");

            migrationBuilder.DropTable(
                name: "funds");
        }
    }
}

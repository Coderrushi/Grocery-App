using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace GroceryStore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Orders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    PaymentMethod = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    PaymentStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MrpTotal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    DeliveryFee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    DeliveryRecipientName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    DeliveryPhoneNumber = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    DeliveryLine1 = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    DeliveryLine2 = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    DeliveryLandmark = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    DeliveryCity = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    DeliveryState = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    DeliveryPincode = table.Column<string>(type: "char(6)", fixedLength: true, maxLength: 6, nullable: false),
                    DeliveryInstructions = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    CancellationReason = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    CheckoutKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    PlacedAtUtc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                    table.CheckConstraint("CK_orders_amounts_not_negative", "`Subtotal` >= 0 AND `DeliveryFee` >= 0");
                    table.CheckConstraint("CK_orders_total", "`Total` = `Subtotal` + `DeliveryFee`");
                    table.ForeignKey(
                        name: "FK_orders_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Brand = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    UnitLabel = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    UnitMrp = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.Id);
                    table.CheckConstraint("CK_order_items_quantity_positive", "`Quantity` > 0");
                    table.ForeignKey(
                        name: "FK_order_items_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "order_status_changes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_status_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_status_changes_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_status_changes_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrderId",
                table: "order_items",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_ProductId",
                table: "order_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_changes_ChangedByUserId",
                table: "order_status_changes",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_changes_OrderId",
                table: "order_status_changes",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_Status_PlacedAtUtc",
                table: "orders",
                columns: new[] { "Status", "PlacedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_UserId_CheckoutKey",
                table: "orders",
                columns: new[] { "UserId", "CheckoutKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_UserId_PlacedAtUtc",
                table: "orders",
                columns: new[] { "UserId", "PlacedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "order_status_changes");

            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}

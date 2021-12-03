using Microsoft.EntityFrameworkCore.Migrations;

namespace Synchronization.Infrastructure.Persistence.Database.Migrations
{
    public partial class AddDatawallet : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "SyncRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DatawalletId",
                table: "DatawalletModifications",
                type: "char(20)",
                nullable: true);

            migrationBuilder.AddColumn<ushort>(
                name: "DatawalletVersion",
                table: "DatawalletModifications",
                type: "int",
                nullable: false,
                defaultValue: (ushort)0);

            migrationBuilder.CreateTable(
                name: "Datawallets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "char(20)", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(36)", nullable: false),
                    Version = table.Column<ushort>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Datawallets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatawalletModifications_DatawalletId",
                table: "DatawalletModifications",
                column: "DatawalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Datawallets_Owner",
                table: "Datawallets",
                column: "Owner",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DatawalletModifications_Datawallets_DatawalletId",
                table: "DatawalletModifications",
                column: "DatawalletId",
                principalTable: "Datawallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DatawalletModifications_Datawallets_DatawalletId",
                table: "DatawalletModifications");

            migrationBuilder.DropTable(
                name: "Datawallets");

            migrationBuilder.DropIndex(
                name: "IX_DatawalletModifications_DatawalletId",
                table: "DatawalletModifications");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "SyncRuns");

            migrationBuilder.DropColumn(
                name: "DatawalletId",
                table: "DatawalletModifications");

            migrationBuilder.DropColumn(
                name: "DatawalletVersion",
                table: "DatawalletModifications");
        }
    }
}

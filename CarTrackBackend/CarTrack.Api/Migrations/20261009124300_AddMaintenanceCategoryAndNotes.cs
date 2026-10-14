using CarTrack.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarTrack.Api.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261009124300_AddMaintenanceCategoryAndNotes")]
public partial class AddMaintenanceCategoryAndNotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Category",
            table: "Maintenances",
            type: "text",
            nullable: false,
            defaultValue: "Bakım");

        migrationBuilder.AddColumn<string>(
            name: "Notes",
            table: "Maintenances",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Category",
            table: "Maintenances");

        migrationBuilder.DropColumn(
            name: "Notes",
            table: "Maintenances");
    }
}

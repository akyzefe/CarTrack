using CarTrack.Api.Data;
using CarTrack.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarTrack.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaintenancesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public MaintenancesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Sadece seçilen araca ait bakımları getir
    [HttpGet("vehicle/{vehicleId}")]
    public async Task<IActionResult> GetMaintenances(int vehicleId)
    {
        var maintenances = await _context.Maintenances
            .Where(m => m.VehicleId == vehicleId)
            .OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.Id)
            .ToListAsync();
        return Ok(maintenances);
    }

    // Araca yeni bakım ekle
    [HttpPost]
    public async Task<IActionResult> AddMaintenance(Maintenance maintenance)
    {
        if (string.IsNullOrWhiteSpace(maintenance.Category))
            return BadRequest("Harcama türü zorunludur.");

        if (string.IsNullOrWhiteSpace(maintenance.Title))
            return BadRequest("İşlem açıklaması zorunludur.");

        if (maintenance.Cost <= 0)
            return BadRequest("Tutar sıfırdan büyük olmalıdır.");

        if (maintenance.AtKm < 0)
            return BadRequest("Kilometre sıfırdan küçük olamaz.");

        if (maintenance.DueAtKm is < 0)
            return BadRequest("Hatırlatma kilometresi sıfırdan küçük olamaz.");

        if (maintenance.DueAtKm is not null && maintenance.DueAtKm < maintenance.AtKm)
            return BadRequest("Hatırlatma kilometresi, işlem kilometresinden düşük olamaz.");

        var vehicle = await _context.Vehicles.FindAsync(maintenance.VehicleId);
        if (vehicle is null)
            return NotFound("Araç bulunamadı.");

        if (maintenance.DueDate is not null)
            maintenance.DueDate = DateTime.SpecifyKind(maintenance.DueDate.Value.Date, DateTimeKind.Utc);

        maintenance.Date = DateTime.SpecifyKind(maintenance.Date, DateTimeKind.Utc);
        _context.Maintenances.Add(maintenance);
        vehicle.CurrentKm = Math.Max(vehicle.CurrentKm, maintenance.AtKm);
        await _context.SaveChangesAsync();
        return Ok(maintenance);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteMaintenance(int id)
    {
        var maintenance = await _context.Maintenances.FindAsync(id);
        if (maintenance is null)
            return NotFound("Bakım kaydı bulunamadı.");

        _context.Maintenances.Remove(maintenance);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
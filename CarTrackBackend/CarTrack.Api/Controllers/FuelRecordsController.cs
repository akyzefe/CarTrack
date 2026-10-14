using CarTrack.Api.Data;
using CarTrack.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarTrack.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FuelRecordsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public FuelRecordsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("vehicle/{vehicleId:int}")]
    public async Task<IActionResult> GetFuelRecords(int vehicleId)
    {
        if (!await _context.Vehicles.AnyAsync(vehicle => vehicle.Id == vehicleId))
            return NotFound("Araç bulunamadı.");

        var records = await _context.FuelRecords
            .Where(record => record.VehicleId == vehicleId)
            .OrderByDescending(record => record.Date)
            .ToListAsync();

        return Ok(records);
    }

    [HttpPost]
    public async Task<IActionResult> AddFuelRecord(FuelRecord record)
    {
        if (record.AtKm < 0)
            return BadRequest("Kilometre sıfırdan küçük olamaz.");

        if (record.Liters <= 0 || record.Cost <= 0)
            return BadRequest("Litre ve tutar sıfırdan büyük olmalıdır.");

        var vehicle = await _context.Vehicles.FindAsync(record.VehicleId);
        if (vehicle is null)
            return NotFound("Araç bulunamadı.");

        var latestRecord = await _context.FuelRecords
            .Where(existing => existing.VehicleId == record.VehicleId)
            .OrderByDescending(existing => existing.AtKm)
            .FirstOrDefaultAsync();

        if (latestRecord is not null && record.AtKm < latestRecord.AtKm)
            return BadRequest("Kilometre, önceki yakıt kaydından düşük olamaz.");

        record.Date = DateTime.SpecifyKind(record.Date, DateTimeKind.Utc);
        vehicle.CurrentKm = Math.Max(vehicle.CurrentKm, record.AtKm);
        _context.FuelRecords.Add(record);
        await _context.SaveChangesAsync();

        return Ok(record);
    }
}

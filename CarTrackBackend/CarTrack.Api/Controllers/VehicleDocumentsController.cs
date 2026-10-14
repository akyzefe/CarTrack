using CarTrack.Api.Data;
using CarTrack.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarTrack.Api.Controllers;

[ApiController]
[Route("api/vehicle-documents")]
public class VehicleDocumentsController : ControllerBase
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.Ordinal)
    {
        "Muayene",
        "Trafik sigortası",
        "Kasko"
    };

    private readonly ApplicationDbContext _context;

    public VehicleDocumentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("vehicle/{vehicleId:int}")]
    public async Task<IActionResult> GetVehicleDocuments(int vehicleId)
    {
        if (!await _context.Vehicles.AnyAsync(vehicle => vehicle.Id == vehicleId))
            return NotFound("Araç bulunamadı.");

        var documents = await _context.VehicleDocuments
            .Where(document => document.VehicleId == vehicleId)
            .OrderBy(document => document.ExpiryDate)
            .ToListAsync();

        return Ok(documents);
    }

    [HttpPut("vehicle/{vehicleId:int}")]
    public async Task<IActionResult> SaveVehicleDocuments(int vehicleId, VehicleDocument document)
    {
        if (!await _context.Vehicles.AnyAsync(vehicle => vehicle.Id == vehicleId))
            return NotFound("Araç bulunamadı.");

        if (!SupportedTypes.Contains(document.Type))
            return BadRequest("Desteklenmeyen belge türü.");

        var existingDocument = await _context.VehicleDocuments
            .SingleOrDefaultAsync(existing =>
                existing.VehicleId == vehicleId && existing.Type == document.Type);
        if (existingDocument is null)
        {
            document.VehicleId = vehicleId;
            document.ExpiryDate = DateTime.SpecifyKind(document.ExpiryDate.Date, DateTimeKind.Utc);
            _context.VehicleDocuments.Add(document);
        }
        else
        {
            existingDocument.ExpiryDate = DateTime.SpecifyKind(document.ExpiryDate.Date, DateTimeKind.Utc);
        }

        await _context.SaveChangesAsync();
        return await GetVehicleDocuments(vehicleId);
    }
}

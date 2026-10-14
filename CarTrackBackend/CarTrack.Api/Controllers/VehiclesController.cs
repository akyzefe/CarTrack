using CarTrack.Api.Data;
using CarTrack.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarTrack.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // Bu, adresi "api/vehicles" yapar
public class VehiclesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public VehiclesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // 1. Veritabanındaki tüm araçları listele (GET isteği)
    [HttpGet]
    public async Task<IActionResult> GetVehicles()
    {
        var vehicles = await _context.Vehicles.ToListAsync();
        return Ok(vehicles);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetVehicle(int id)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        return vehicle is null ? NotFound() : Ok(vehicle);
    }

    // 2. Veritabanına yeni bir araç ekle (POST isteği)
    [HttpPost]
    public async Task<IActionResult> AddVehicle(Vehicle vehicle)
    {
        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync();
        return Ok(vehicle);
    }

    [HttpPut("{id:int}/current-km")]
    public async Task<IActionResult> UpdateCurrentKm(int id, [FromBody] int currentKm)
    {
        if (currentKm < 0)
            return BadRequest("Kilometre sıfırdan küçük olamaz.");

        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle is null)
            return NotFound("Araç bulunamadı.");

        vehicle.CurrentKm = currentKm;
        await _context.SaveChangesAsync();
        return Ok(vehicle);
    }

    // 3. Veritabanından araç sil (DELETE isteği)
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteVehicle(int id)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null) return NotFound();

        _context.Vehicles.Remove(vehicle);
        await _context.SaveChangesAsync();
        return NoContent(); // Başarılı ama geri dönecek veri yok mesajı (204)
    }
}   
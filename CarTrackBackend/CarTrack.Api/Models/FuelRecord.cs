namespace CarTrack.Api.Models;

public class FuelRecord
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public int AtKm { get; set; }
    public decimal Liters { get; set; }
    public decimal Cost { get; set; }
    public bool IsFullTank { get; set; }
}

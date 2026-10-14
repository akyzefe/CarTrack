namespace CarTrack.Api.Models;

public class Maintenance
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public string Category { get; set; } = "Bakım";
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public int AtKm { get; set; }
    public decimal Cost { get; set; }
    public DateTime? DueDate { get; set; }
    public int? DueAtKm { get; set; }
}
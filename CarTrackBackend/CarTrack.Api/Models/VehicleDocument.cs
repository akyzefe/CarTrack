namespace CarTrack.Api.Models;

public class VehicleDocument
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
}

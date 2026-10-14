namespace CarTrack.Api.Models;

public class Vehicle
{
    public int Id { get; set; }             // Veritabanındaki benzersiz kimlik (örn: 1)
    public int UserId { get; set; }         // Bu araç hangi kullanıcıya ait? (İlerideki User tablosu için)
    public string Brand { get; set; } = string.Empty;  // Marka (Örn: Peugeot)
    public string Model { get; set; } = string.Empty;  // Model (Örn: 207)
    public int Year { get; set; }           // Yıl (Örn: 2008)
    public string Engine { get; set; } = string.Empty; // Motor (Örn: 1.6 HDi)
    public int CurrentKm { get; set; }      // Mevcut Kilometre (Örn: 162340)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Kayıt tarihi
}
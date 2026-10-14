using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarTrackApp;

public class VehicleDto : INotifyPropertyChanged
{
    private int _currentKm;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int id { get; set; }
    public int userId { get; set; }
    public string? brand { get; set; }
    public string? model { get; set; }
    public int year { get; set; }
    public string? engine { get; set; }
    public int currentKm
    {
        get => _currentKm;
        set
        {
            if (_currentKm == value)
                return;

            _currentKm = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(currentKm)));
        }
    }

    public string BrandBadgeText => brand switch
    {
        "BMW" => "BMW",
        "Mercedes-Benz" => "MB",
        "Peugeot" => "P",
        "Volkswagen" => "VW",
        _ => string.IsNullOrWhiteSpace(brand) ? "?" : brand[..1].ToUpperInvariant()
    };

#if DEBUG
    public string? BrandLogoSource => string.IsNullOrWhiteSpace(brand)
        ? null
        : $"{new string(brand.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray())}.png";
#else
    public string? BrandLogoSource => null;
#endif

    public bool HasBrandLogo => BrandLogoSource is not null;
    public bool ShowBrandBadge => !HasBrandLogo;
    public override string ToString() => $"{brand} {model} ({year})";
}

public sealed record AiMechanicChatMessage(string Role, string Content, string? Disclaimer = null)
{
    public string Sender => Role == "user" ? "Sen" : "AI Mechanic";
    public bool HasDisclaimer => !string.IsNullOrWhiteSpace(Disclaimer);
}

internal sealed class AiMechanicChatTurnDto
{
    public string role { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
}

internal sealed class AiMechanicChatRequestDto
{
    public int vehicleId { get; set; }
    public string question { get; set; } = string.Empty;
    public List<AiMechanicChatTurnDto> history { get; set; } = [];
}

internal sealed class AiMechanicChatResponseDto
{
    public string answer { get; set; } = string.Empty;
    public string disclaimer { get; set; } = string.Empty;
}

internal sealed class ApiProblemDto
{
    public string? title { get; set; }
    public string? detail { get; set; }
    public Dictionary<string, string[]>? errors { get; set; }
}

internal sealed class MaintenanceSummaryDto
{
    public DateTime date { get; set; }
    public decimal cost { get; set; }
}

internal sealed class FuelSummaryDto
{
    public DateTime date { get; set; }
    public decimal cost { get; set; }
}

public sealed class WeatherLocation
{
    public string Name { get; init; } = string.Empty;
    public string? AdministrativeArea { get; init; }
    public string? Country { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string DisplayName => string.Join(", ",
        new[] { Name, AdministrativeArea, Country }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.OrdinalIgnoreCase));
}

internal sealed class GeocodingResponse
{
    public List<GeocodingResult>? Results { get; set; }
}

internal sealed class GeocodingResult
{
    public string Name { get; set; } = string.Empty;
    public string? Admin1 { get; set; }
    public string? Country { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

internal sealed class WeatherForecastResponse
{
    public CurrentWeather? Current { get; set; }
    public DailyWeather? Daily { get; set; }
}

internal sealed class CurrentWeather
{
    [JsonPropertyName("temperature_2m")]
    public double Temperature2m { get; set; }

    [JsonPropertyName("weather_code")]
    public int WeatherCode { get; set; }
}

internal sealed class DailyWeather
{
    [JsonPropertyName("precipitation_probability_max")]
    public List<int>? PrecipitationProbabilityMax { get; set; }

    [JsonPropertyName("precipitation_sum")]
    public List<double>? PrecipitationSum { get; set; }
}

public partial class MainViewModel : ObservableObject
{
    private const string OtherOption = "Diğer";

    private const string WeatherLatitudeKey = "weather.location.latitude";
    private const string WeatherLongitudeKey = "weather.location.longitude";
    private const string WeatherLocationNameKey = "weather.location.name";
    private const string VehicleOrderPreferenceKey = "garage.vehicle-order";

    private static readonly Dictionary<string, Dictionary<string, string[]>> VehicleCatalog = new()
    {
        ["Audi"] = new()
        {
            ["A3"] = ["1.0 TFSI", "1.4 TFSI", "1.6 TDI", "2.0 TDI"],
            ["A4"] = ["1.4 TFSI", "1.8 TFSI", "2.0 TFSI", "2.0 TDI"],
            ["A5"] = ["2.0 TFSI", "2.0 TDI", "3.0 TDI"],
            ["A6"] = ["2.0 TFSI", "2.0 TDI", "3.0 TDI"],
            ["A8"] = ["3.0 TFSI", "3.0 TDI", "4.0 TFSI"],
            ["Q2"] = ["1.0 TFSI", "1.5 TFSI", "1.6 TDI"],
            ["Q3"] = ["1.4 TFSI", "1.5 TFSI", "1.6 TDI", "2.0 TDI"],
            ["Q5"] = ["2.0 TFSI", "2.0 TDI", "3.0 TDI"],
            ["Q7"] = ["3.0 TFSI", "3.0 TDI", "4.0 TDI"],
            ["TT"] = ["1.8 TFSI", "2.0 TFSI", "2.0 TDI"]
        },
        ["BMW"] = new()
        {
            ["1 Serisi"] = ["1.5 118i", "2.0 120d", "2.0 118d"],
            ["2 Serisi"] = ["1.5 218i", "2.0 220i", "2.0 220d"],
            ["3 Serisi"] = ["1.6 316i", "2.0 318i", "2.0 320i", "2.0 320d"],
            ["4 Serisi"] = ["2.0 420i", "2.0 420d", "3.0 430d"],
            ["5 Serisi"] = ["2.0 520i", "2.0 520d", "3.0 530d"],
            ["X1"] = ["1.5 sDrive18i", "2.0 xDrive20i", "2.0 xDrive18d"],
            ["X3"] = ["2.0 xDrive20i", "2.0 xDrive20d", "3.0 xDrive30d"],
            ["X5"] = ["2.0 xDrive25d", "3.0 xDrive30d", "3.0 xDrive40i"],
            ["X6"] = ["3.0 xDrive30d", "3.0 xDrive40i"]
        },
        ["Citroen"] = new()
        {
            ["Berlingo"] = ["1.2 PureTech", "1.5 BlueHDi", "1.6 HDi"],
            ["C1"] = ["1.0 VTi", "1.2 PureTech"],
            ["C2"] = ["1.1", "1.4", "1.4 HDi", "1.6 VTS"],
            ["C3"] = ["1.2 PureTech", "1.4 HDi", "1.6 HDi"],
            ["C4"] = ["1.2 PureTech", "1.6 VTi", "1.6 HDi", "1.5 BlueHDi"],
            ["C4 Cactus"] = ["1.2 PureTech", "1.6 BlueHDi"],
            ["C5"] = ["1.6 THP", "1.6 HDi", "2.0 HDi", "2.2 HDi"],
            ["C5 Aircross"] = ["1.2 PureTech", "1.6 PureTech", "1.5 BlueHDi"],
            ["C-Elysee"] = ["1.2 VTi", "1.6 VTi", "1.6 HDi"],
            ["Jumper"] = ["2.0 BlueHDi", "2.2 BlueHDi", "2.2 HDi"]
        },
        ["Dacia"] = new()
        {
            ["Duster"] = ["1.0 TCe", "1.3 TCe", "1.5 dCi", "1.6"],
            ["Bigster"] = ["1.2 TCe Mild Hybrid", "1.6 Hybrid"],
            ["Jogger"] = ["1.0 TCe", "1.0 ECO-G", "1.6 Hybrid"],
            ["Lodgy"] = ["1.5 dCi", "1.6"],
            ["Logan"] = ["0.9 TCe", "1.0 SCe", "1.5 dCi"],
            ["Sandero"] = ["0.9 TCe", "1.0 SCe", "1.0 TCe", "1.5 dCi"],
            ["Spring"] = ["Electric"]
        },
        ["Fiat"] = new()
        {
            ["500"] = ["1.0 Hybrid", "1.2 Fire", "0.9 TwinAir", "1.3 MultiJet"],
            ["Doblo"] = ["1.3 MultiJet", "1.6 MultiJet", "2.0 MultiJet"],
            ["Ducato"] = ["2.2 MultiJet", "2.3 MultiJet"],
            ["Egea"] = ["1.4 Fire", "1.3 MultiJet", "1.6 MultiJet"],
            ["Fiorino"] = ["1.3 MultiJet", "1.4 Fire"],
            ["Punto"] = ["1.2 Fire", "1.4 Fire", "1.3 MultiJet"],
            ["Bravo"] = ["1.4 T-Jet", "1.6 MultiJet", "1.9 MultiJet"],
            ["Linea"] = ["1.4 Fire", "1.3 MultiJet", "1.6 MultiJet"],
            ["Panda"] = ["1.0 Hybrid", "1.2 Fire", "0.9 TwinAir"],
            ["Tipo"] = ["1.0 FireFly", "1.4 Fire", "1.6 MultiJet"]
        },
        ["Ford"] = new()
        {
            ["Courier"] = ["1.0 EcoBoost", "1.5 TDCi", "1.5 EcoBlue"],
            ["C-Max"] = ["1.0 EcoBoost", "1.6 Ti-VCT", "1.5 TDCi"],
            ["EcoSport"] = ["1.0 EcoBoost", "1.5 Ti-VCT", "1.5 TDCi"],
            ["Fiesta"] = ["1.0 EcoBoost", "1.25 Duratec", "1.4 TDCi", "1.5 TDCi"],
            ["Focus"] = ["1.0 EcoBoost", "1.6 Ti-VCT", "1.5 TDCi", "1.6 TDCi"],
            ["Kuga"] = ["1.5 EcoBoost", "2.0 TDCi", "2.5 Hybrid"],
            ["Mondeo"] = ["1.5 EcoBoost", "2.0 EcoBoost", "2.0 TDCi"],
            ["Puma"] = ["1.0 EcoBoost", "1.0 EcoBoost Hybrid"],
            ["Ranger"] = ["2.0 EcoBlue", "3.0 EcoBlue"],
            ["S-Max"] = ["1.5 EcoBoost", "2.0 EcoBoost", "2.0 TDCi"]
        },
        ["Honda"] = new()
        {
            ["Accord"] = ["1.5 VTEC Turbo", "2.0 i-VTEC", "2.2 i-DTEC"],
            ["Civic"] = ["1.0 VTEC Turbo", "1.5 VTEC Turbo", "1.6 i-DTEC"],
            ["CR-Z"] = ["1.5 i-VTEC Hybrid"],
            ["Jazz"] = ["1.3 i-VTEC", "1.5 i-VTEC", "1.5 e:HEV"],
            ["HR-V"] = ["1.5 i-VTEC", "1.5 e:HEV"],
            ["CR-V"] = ["1.5 VTEC Turbo", "2.0 i-MMD Hybrid", "2.2 i-DTEC"]
        },
        ["Hyundai"] = new()
        {
            ["Bayon"] = ["1.0 T-GDi", "1.2 MPI", "1.4 MPI"],
            ["Kona"] = ["1.0 T-GDi", "1.6 T-GDi", "1.6 Hybrid", "Electric"],
            ["i20"] = ["1.0 T-GDi", "1.2 MPI", "1.4 CRDi"],
            ["i30"] = ["1.0 T-GDi", "1.4 T-GDi", "1.6 CRDi"],
            ["i10"] = ["1.0 MPI", "1.2 MPI"],
            ["Accent"] = ["1.4 MPI", "1.5 CRDi", "1.6 MPI"],
            ["Elantra"] = ["1.6 MPI", "1.6 CRDi", "1.6 Hybrid"],
            ["Santa Fe"] = ["1.6 T-GDi Hybrid", "2.2 CRDi"],
            ["Tucson"] = ["1.6 GDi", "1.6 T-GDi", "1.6 CRDi", "1.6 Hybrid"]
        },
        ["Mazda"] = new()
        {
            ["Mazda2"] = ["1.5 Skyactiv-G", "1.5 Skyactiv-D"],
            ["Mazda3"] = ["1.5 Skyactiv-G", "2.0 Skyactiv-G", "1.8 Skyactiv-D"],
            ["Mazda6"] = ["2.0 Skyactiv-G", "2.5 Skyactiv-G", "2.2 Skyactiv-D"],
            ["MX-5"] = ["1.5 Skyactiv-G", "2.0 Skyactiv-G"],
            ["CX-3"] = ["1.5 Skyactiv-D", "2.0 Skyactiv-G"],
            ["CX-30"] = ["2.0 Skyactiv-G", "2.0 Skyactiv-X"],
            ["CX-5"] = ["2.0 Skyactiv-G", "2.5 Skyactiv-G", "2.2 Skyactiv-D"]
        },
        ["Mercedes-Benz"] = new()
        {
            ["A Serisi"] = ["1.3 Turbo", "1.5 CDI", "1.6 Turbo", "2.0 Turbo"],
            ["B Serisi"] = ["1.3 Turbo", "1.5 CDI", "2.0 CDI"],
            ["C Serisi"] = ["1.6 Kompressor", "1.6 Turbo", "2.0 Turbo", "2.0 CDI"],
            ["CLA"] = ["1.3 Turbo", "1.6 Turbo", "2.0 CDI"],
            ["E Serisi"] = ["2.0 Turbo", "2.0 CDI", "3.0 CDI"],
            ["GLA"] = ["1.3 Turbo", "1.6 Turbo", "2.0 CDI"],
            ["GLC"] = ["2.0 Turbo", "2.0 CDI", "3.0 CDI"],
            ["GLE"] = ["2.0 Turbo", "2.0 CDI", "3.0 CDI"],
            ["GLK"] = ["2.0 Turbo", "2.2 CDI", "3.0 CDI"],
            ["Vito"] = ["1.6 CDI", "2.0 CDI", "2.2 CDI"]
        },
        ["Opel"] = new()
        {
            ["Astra"] = ["1.2 Turbo", "1.4 Turbo", "1.6", "1.6 CDTI", "1.5 Diesel"],
            ["Corsa"] = ["1.2", "1.2 Turbo", "1.4", "1.3 CDTI"],
            ["Crossland"] = ["1.2 Turbo", "1.5 Diesel"],
            ["Grandland"] = ["1.2 Turbo", "1.6 Turbo Hybrid", "1.5 Diesel"],
            ["Insignia"] = ["1.5 Turbo", "1.6 Turbo", "2.0 CDTI"],
            ["Mokka"] = ["1.2 Turbo", "1.5 Diesel", "Electric"],
            ["Meriva"] = ["1.4", "1.4 Turbo", "1.7 CDTI"],
            ["Zafira"] = ["1.4 Turbo", "1.6 Turbo", "2.0 CDTI"]
        },
        ["Peugeot"] = new()
        {
            ["206"] = ["1.4", "1.4 HDi", "1.6"],
            ["207"] = ["1.4", "1.4 HDi", "1.6 VTi", "1.6 HDi"],
            ["208"] = ["1.2 PureTech", "1.4 HDi", "1.6 BlueHDi"],
            ["301"] = ["1.2 VTi", "1.6 VTi", "1.6 HDi"],
            ["307"] = ["1.6", "1.6 HDi", "2.0 HDi"],
            ["308"] = ["1.2 PureTech", "1.6 VTi", "1.6 HDi", "1.6 BlueHDi"],
            ["407"] = ["1.8", "2.0", "2.0 HDi", "2.2 HDi"],
            ["408"] = ["1.2 PureTech", "1.6 PureTech", "1.5 BlueHDi"],
            ["508"] = ["1.6 THP", "1.5 BlueHDi", "1.6 BlueHDi", "2.0 BlueHDi"],
            ["2008"] = ["1.2 PureTech", "1.5 BlueHDi", "1.6 BlueHDi"],
            ["3008"] = ["1.2 PureTech", "1.6 THP", "1.5 BlueHDi", "1.6 BlueHDi"],
            ["5008"] = ["1.2 PureTech", "1.6 PureTech", "1.5 BlueHDi", "2.0 BlueHDi"],
            ["Partner"] = ["1.2 PureTech", "1.5 BlueHDi", "1.6 HDi"],
            ["Rifter"] = ["1.2 PureTech", "1.5 BlueHDi"],
            ["Expert"] = ["1.5 BlueHDi", "2.0 BlueHDi"]
        },
        ["Kia"] = new()
        {
            ["Niro"] = ["1.6 Hybrid", "1.6 Plug-in Hybrid", "Electric"],
            ["Ceed"] = ["1.0 T-GDi", "1.4 T-GDi", "1.6 CRDi"],
            ["Rio"] = ["1.0 T-GDi", "1.25", "1.4 CRDi"],
            ["Picanto"] = ["1.0 MPI", "1.2 MPI"],
            ["Sorento"] = ["1.6 T-GDi Hybrid", "2.2 CRDi"],
            ["Sportage"] = ["1.6 GDI", "1.6 T-GDi", "1.6 CRDi", "1.6 Hybrid"],
            ["Stonic"] = ["1.0 T-GDi", "1.2 MPI", "1.4 MPI"]
        },
        ["Renault"] = new()
        {
            ["Clio"] = ["0.9 TCe", "1.2", "1.5 dCi"],
            ["Captur"] = ["1.0 TCe", "1.3 TCe", "1.5 dCi", "1.6 E-Tech Hybrid"],
            ["Arkana"] = ["1.3 TCe", "1.6 E-Tech Hybrid"],
            ["Austral"] = ["1.2 E-Tech Hybrid", "1.3 TCe"],
            ["Kadjar"] = ["1.3 TCe", "1.5 dCi", "1.6 dCi"],
            ["Fluence"] = ["1.6", "1.5 dCi", "1.6 dCi"],
            ["Kangoo"] = ["1.3 TCe", "1.5 dCi", "1.5 Blue dCi"],
            ["Laguna"] = ["1.5 dCi", "2.0 Turbo", "2.0 dCi"],
            ["Megane"] = ["1.2 TCe", "1.6", "1.5 dCi", "1.6 dCi"],
            ["Scenic"] = ["1.3 TCe", "1.5 dCi", "1.6 dCi"],
            ["Talisman"] = ["1.3 TCe", "1.6 TCe", "1.5 dCi", "1.6 dCi"],
            ["Twingo"] = ["1.0 SCe", "0.9 TCe"],
            ["Symbol"] = ["1.2", "1.4", "1.5 dCi"]
        },
        ["Seat"] = new()
        {
            ["Arona"] = ["1.0 MPI", "1.0 TSI", "1.5 TSI", "1.6 TDI"],
            ["Ateca"] = ["1.0 TSI", "1.5 TSI", "1.6 TDI", "2.0 TDI"],
            ["Ibiza"] = ["1.0 MPI", "1.0 TSI", "1.2 TSI", "1.6 TDI"],
            ["Leon"] = ["1.0 TSI", "1.5 TSI", "1.6 TDI", "2.0 TDI"],
            ["Tarraco"] = ["1.5 TSI", "2.0 TSI", "2.0 TDI"]
        },
        ["Skoda"] = new()
        {
            ["Fabia"] = ["1.0 MPI", "1.0 TSI", "1.2 TSI", "1.4 TDI"],
            ["Kamiq"] = ["1.0 TSI", "1.5 TSI", "1.6 TDI"],
            ["Karoq"] = ["1.0 TSI", "1.5 TSI", "1.6 TDI", "2.0 TDI"],
            ["Kodiaq"] = ["1.5 TSI", "2.0 TSI", "2.0 TDI"],
            ["Octavia"] = ["1.0 TSI", "1.5 TSI", "1.6 TDI", "2.0 TDI"],
            ["Rapid"] = ["1.0 TSI", "1.2 TSI", "1.4 TSI", "1.6 TDI"],
            ["Roomster"] = ["1.2 TSI", "1.4 TDI", "1.6 TDI"],
            ["Superb"] = ["1.4 TSI", "1.5 TSI", "1.6 TDI", "2.0 TDI"]
        },
        ["Toyota"] = new()
        {
            ["Auris"] = ["1.33 VVT-i", "1.6 Valvematic", "1.4 D-4D", "1.8 Hybrid"],
            ["C-HR"] = ["1.2 Turbo", "1.8 Hybrid", "2.0 Hybrid"],
            ["Camry"] = ["2.0 VVT-i", "2.5 Hybrid"],
            ["Corolla"] = ["1.33 Dual VVT-i", "1.6 Valvematic", "1.4 D-4D", "1.8 Hybrid"],
            ["Avensis"] = ["1.6 Valvematic", "1.8 Valvematic", "2.0 D-4D"],
            ["Aygo"] = ["1.0 VVT-i"],
            ["Hilux"] = ["2.4 D-4D", "2.8 D-4D"],
            ["RAV4"] = ["2.0 Valvematic", "2.2 D-4D", "2.5 Hybrid"],
            ["Yaris"] = ["1.0 VVT-i", "1.33 Dual VVT-i", "1.4 D-4D", "1.5 Hybrid"]
        },
        ["Volkswagen"] = new()
        {
            ["Arteon"] = ["1.5 TSI", "2.0 TSI", "2.0 TDI"],
            ["Caddy"] = ["1.5 TSI", "2.0 TDI"],
            ["Golf"] = ["1.0 TSI", "1.4 TSI", "1.5 TSI", "1.6 TDI", "2.0 TDI"],
            ["Passat"] = ["1.4 TSI", "1.5 TSI", "1.6 TDI", "2.0 TDI"],
            ["Polo"] = ["1.0 MPI", "1.0 TSI", "1.4 TDI", "1.6 TDI"],
            ["T-Cross"] = ["1.0 TSI", "1.5 TSI"],
            ["T-Roc"] = ["1.0 TSI", "1.5 TSI", "2.0 TDI"],
            ["Tiguan"] = ["1.5 TSI", "2.0 TSI", "2.0 TDI"],
            ["Touareg"] = ["3.0 TDI", "3.0 TSI"],
            ["Jetta"] = ["1.4 TSI", "1.6 TDI", "2.0 TDI"],
            ["Touran"] = ["1.4 TSI", "1.6 TDI", "2.0 TDI"],
            ["Transporter"] = ["2.0 TDI", "2.0 TSI"]
        },
        ["Volvo"] = new()
        {
            ["S60"] = ["2.0 T4", "2.0 T5", "2.0 D4", "2.0 B4"],
            ["S90"] = ["2.0 T5", "2.0 T6", "2.0 D4", "2.0 B5"],
            ["V40"] = ["1.5 T3", "2.0 T4", "2.0 D2", "2.0 D4"],
            ["V60"] = ["2.0 T4", "2.0 T5", "2.0 D4", "2.0 B4"],
            ["XC40"] = ["1.5 T3", "2.0 T4", "2.0 D3", "Electric"],
            ["XC60"] = ["2.0 T5", "2.0 T6", "2.0 D4", "2.0 B5"],
            ["XC90"] = ["2.0 T5", "2.0 T6", "2.0 D5", "2.0 T8 Hybrid"]
        },
        ["Alfa Romeo"] = new()
        {
            ["Giulia"] = ["2.0 Turbo", "2.2 Diesel", "2.9 V6"],
            ["Giulietta"] = ["1.4 TB", "1.6 JTDm", "2.0 JTDm"],
            ["Stelvio"] = ["2.0 Turbo", "2.2 Diesel", "2.9 V6"]
        },
        ["Chevrolet"] = new()
        {
            ["Aveo"] = ["1.2", "1.4", "1.3 Diesel"],
            ["Captiva"] = ["2.0 Diesel", "2.2 Diesel", "2.4"],
            ["Cruze"] = ["1.4 Turbo", "1.6", "1.7 Diesel", "2.0 Diesel"],
            ["Spark"] = ["1.0", "1.2"],
            ["Trax"] = ["1.4 Turbo", "1.6", "1.7 Diesel"]
        },
        ["Cupra"] = new()
        {
            ["Ateca"] = ["1.5 TSI", "2.0 TSI"],
            ["Born"] = ["Electric"],
            ["Formentor"] = ["1.5 TSI", "2.0 TSI", "2.0 TDI", "1.4 e-Hybrid"],
            ["Leon"] = ["1.5 TSI", "2.0 TSI", "2.0 TDI", "1.4 e-Hybrid"]
        },
        ["DS Automobiles"] = new()
        {
            ["DS 3"] = ["1.2 PureTech", "1.5 BlueHDi", "Electric"],
            ["DS 4"] = ["1.2 PureTech", "1.5 BlueHDi", "1.6 E-Tense"],
            ["DS 7"] = ["1.2 PureTech", "1.5 BlueHDi", "1.6 E-Tense"]
        },
        ["Jeep"] = new()
        {
            ["Avenger"] = ["1.2 Turbo", "Electric"],
            ["Cherokee"] = ["2.0 Turbo", "2.2 Multijet", "3.2 V6"],
            ["Compass"] = ["1.3 Turbo", "1.6 Multijet", "2.0 Multijet"],
            ["Renegade"] = ["1.0 Turbo", "1.3 Turbo", "1.6 Multijet"],
            ["Wrangler"] = ["2.0 Turbo", "2.2 Multijet", "3.6 V6"]
        },
        ["MINI"] = new()
        {
            ["Clubman"] = ["1.5 Cooper", "2.0 Cooper S", "2.0 Cooper D"],
            ["Countryman"] = ["1.5 Cooper", "2.0 Cooper S", "2.0 Cooper D", "1.5 Hybrid"],
            ["Cabrio"] = ["1.5 Cooper", "2.0 Cooper S"],
            ["Hatch"] = ["1.5 Cooper", "2.0 Cooper S", "2.0 Cooper D"]
        },
        ["Nissan"] = new()
        {
            ["Juke"] = ["1.0 DIG-T", "1.2 DIG-T", "1.5 dCi"],
            ["Micra"] = ["0.9 IG-T", "1.0 IG-T", "1.2"],
            ["Qashqai"] = ["1.2 DIG-T", "1.3 DIG-T", "1.5 dCi", "1.6 dCi"],
            ["X-Trail"] = ["1.3 DIG-T", "1.6 dCi", "2.0 dCi", "2.5 Hybrid"]
        },
        ["Porsche"] = new()
        {
            ["Cayenne"] = ["3.0 V6", "2.9 V6", "4.0 V8", "E-Hybrid"],
            ["Macan"] = ["2.0 Turbo", "2.9 V6"],
            ["Panamera"] = ["2.9 V6", "3.0 V6", "4.0 V8", "E-Hybrid"]
        },
        ["Subaru"] = new()
        {
            ["Forester"] = ["2.0 Boxer", "2.0 e-Boxer", "2.0 Diesel"],
            ["Impreza"] = ["1.6 Boxer", "2.0 Boxer"],
            ["Outback"] = ["2.5 Boxer", "2.0 Diesel"],
            ["XV"] = ["1.6 Boxer", "2.0 Boxer", "2.0 e-Boxer"]
        },
        ["Tesla"] = new()
        {
            ["Model 3"] = ["Standard Range", "Long Range", "Performance"],
            ["Model S"] = ["Long Range", "Plaid"],
            ["Model X"] = ["Long Range", "Plaid"],
            ["Model Y"] = ["Standard Range", "Long Range", "Performance"]
        },
        ["Jaguar"] = new()
        {
            ["E-Pace"] = ["2.0 Turbo", "2.0 D"],
            ["F-Pace"] = ["2.0 Turbo", "2.0 D", "3.0 D"],
            ["F-Type"] = ["2.0 Turbo", "3.0 V6", "5.0 V8"],
            ["XE"] = ["2.0 Turbo", "2.0 D"],
            ["XF"] = ["2.0 Turbo", "2.0 D", "3.0 D"]
        },
        ["Land Rover"] = new()
        {
            ["Defender"] = ["2.0 D", "3.0 D", "3.0 P"],
            ["Discovery"] = ["2.0 D", "3.0 D", "3.0 P"],
            ["Discovery Sport"] = ["2.0 D", "2.0 P"],
            ["Range Rover"] = ["3.0 D", "3.0 P", "4.4 V8"],
            ["Range Rover Evoque"] = ["1.5 PHEV", "2.0 D", "2.0 P"],
            ["Range Rover Sport"] = ["3.0 D", "3.0 P", "4.4 V8"]
        },
        ["Lexus"] = new()
        {
            ["CT"] = ["1.8 Hybrid"],
            ["ES"] = ["2.5 Hybrid", "3.5 V6"],
            ["LBX"] = ["1.5 Hybrid"],
            ["NX"] = ["2.5 Hybrid", "2.5 Plug-in Hybrid"],
            ["RX"] = ["2.5 Hybrid", "2.4 Turbo Hybrid", "3.5 V6"],
            ["UX"] = ["2.0 Hybrid", "Electric"]
        },
        ["Mitsubishi"] = new()
        {
            ["ASX"] = ["1.0 Turbo", "1.3 Turbo", "1.6", "1.8 DI-D"],
            ["Eclipse Cross"] = ["1.5 Turbo", "2.2 DI-D", "2.4 Plug-in Hybrid"],
            ["L200"] = ["2.2 DI-D", "2.4 DI-D"],
            ["Outlander"] = ["2.0", "2.2 DI-D", "2.4 Plug-in Hybrid"],
            ["Pajero"] = ["3.2 DI-D", "3.8 V6"],
            ["Space Star"] = ["1.0", "1.2"]
        }
    };

    private static readonly HttpClient Client = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly Dictionary<int, ObservableCollection<AiMechanicChatMessage>> _aiMechanicChats = new();

    [ObservableProperty]
    private ObservableCollection<VehicleDto> vehicles = new();

    [ObservableProperty]
    private VehicleDto? selectedAiVehicle;

    [ObservableProperty]
    private ObservableCollection<AiMechanicChatMessage> aiMechanicMessages = new();

    [ObservableProperty]
    private string aiMechanicQuestion = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAiMechanicClosed))]
    private bool isAiMechanicOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAiMechanicButtonHidden))]
    private bool isAiMechanicButtonVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotAiMechanicBusy))]
    private bool isAiMechanicBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiMechanicError))]
    private string? aiMechanicErrorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotLoading))]
    [NotifyCanExecuteChangedFor(nameof(AddVehicleCommand))]
    private bool isLoading;

    [ObservableProperty]
    private bool isMonthlyExpensesLoading;

    public bool IsNotMonthlyExpensesLoading => !IsMonthlyExpensesLoading;

    [ObservableProperty]
    private bool hasMonthlyExpensesError;

    [ObservableProperty]
    private string monthlyExpensesTotal = "—";

    [ObservableProperty]
    private string monthlyExpensesDetails = "Bu ayki harcamalar";

    [ObservableProperty]
    private string? monthlyExpensesErrorMessage;

    public ObservableCollection<string> BrandOptions { get; } =
        new(VehicleCatalog.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).Append(OtherOption));
    private ObservableCollection<string> _modelOptions = new();
    private ObservableCollection<string> _engineOptions = new();

    public ObservableCollection<string> ModelOptions
    {
        get => _modelOptions;
        private set => SetProperty(ref _modelOptions, value);
    }

    public ObservableCollection<string> EngineOptions
    {
        get => _engineOptions;
        private set => SetProperty(ref _engineOptions, value);
    }

    [ObservableProperty]
    private string brand = string.Empty;

    [ObservableProperty]
    private string model = string.Empty;

    [ObservableProperty]
    private string year = string.Empty;

    [ObservableProperty]
    private string engine = string.Empty;

    [ObservableProperty]
    private string customBrand = string.Empty;

    [ObservableProperty]
    private string customModel = string.Empty;

    [ObservableProperty]
    private string customEngine = string.Empty;

    [ObservableProperty]
    private string currentKm = string.Empty;

    [ObservableProperty]
    private string locationSearchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<WeatherLocation> locationOptions = new();

    [ObservableProperty]
    private WeatherLocation? selectedWeatherLocation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotWeatherBusy))]
    private bool isWeatherBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeatherLocationButtonText))]
    private bool hasWeather;

    [ObservableProperty]
    private bool hasWeatherError;

    [ObservableProperty]
    private string weatherLocationText = "Şehrini seçerek hava durumunu görüntüle";

    [ObservableProperty]
    private string weatherIcon = "🌤️";

    [ObservableProperty]
    private string weatherSummary = "Konum seç";

    [ObservableProperty]
    private string weatherDetails = "Konum seçildiğinde güncel tahmin burada görünür.";

    [ObservableProperty]
    private string carWashAdvice = "Araç yıkama hava önerisi";

    [ObservableProperty]
    private string carWashAdviceBackground = "#EAF0F7";

    [ObservableProperty]
    private string carWashAdviceColor = "#344258";

    [ObservableProperty]
    private string? weatherErrorMessage;

    private string? _errorMessage;
    private bool _hasLoadError;

    public int VehicleCount => Vehicles.Count;
    public bool IsNotLoading => !IsLoading;
    public bool IsNotAiMechanicBusy => !IsAiMechanicBusy;
    public bool IsAiMechanicClosed => !IsAiMechanicOpen;
    public bool IsAiMechanicButtonHidden => !IsAiMechanicButtonVisible;
    public string SelectedAiVehicleDisplay => SelectedAiVehicle is null
        ? "Araç seçilmedi"
        : $"{SelectedAiVehicle.brand} {SelectedAiVehicle.model} • {SelectedAiVehicle.year}";
    public bool HasAiMechanicError => !string.IsNullOrWhiteSpace(AiMechanicErrorMessage);
    public bool IsNotWeatherBusy => !IsWeatherBusy;
    public string WeatherLocationButtonText => HasWeather ? "Konum" : "Seç";
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasSelectedBrand => !string.IsNullOrWhiteSpace(Brand);
    public bool IsCustomBrand => Brand == OtherOption;
    public bool IsNotCustomBrand => !IsCustomBrand;
    public bool IsCustomModel => Model == OtherOption;
    public bool IsNotCustomModel => !IsCustomModel;
    public bool HasEngineOptions => EngineOptions.Count > 0;
    public bool IsCustomEngine => Engine == OtherOption;
    public bool IsNotCustomEngine => !IsCustomEngine;

    partial void OnBrandChanged(string value)
    {
        OnPropertyChanged(nameof(HasSelectedBrand));
        OnPropertyChanged(nameof(IsCustomBrand));
        OnPropertyChanged(nameof(IsNotCustomBrand));
    }

    partial void OnModelChanged(string value)
    {
        OnPropertyChanged(nameof(IsCustomModel));
        OnPropertyChanged(nameof(IsNotCustomModel));
    }

    partial void OnEngineChanged(string value)
    {
        OnPropertyChanged(nameof(IsCustomEngine));
        OnPropertyChanged(nameof(IsNotCustomEngine));
    }
    public string EmptyMessage => HasLoadError ? "Araç listesi yüklenemedi." : "Garajın henüz boş.";

    partial void OnSelectedAiVehicleChanged(VehicleDto? value)
    {
        OnPropertyChanged(nameof(SelectedAiVehicleDisplay));
        AiMechanicErrorMessage = null;
        AiMechanicQuestion = string.Empty;

        if (value is null)
        {
            AiMechanicMessages = new ObservableCollection<AiMechanicChatMessage>();
            return;
        }

        if (!_aiMechanicChats.TryGetValue(value.id, out var chat))
        {
            chat = new ObservableCollection<AiMechanicChatMessage>
            {
                new(
                    "assistant",
                    $"{value.brand} {value.model} hakkında teknik özellikleri, sık görülen sorunları veya yaşadığın belirtileri sorabilirsin.")
            };
            _aiMechanicChats.Add(value.id, chat);
        }

        AiMechanicMessages = chat;
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasLoadError
    {
        get => _hasLoadError;
        private set
        {
            if (SetProperty(ref _hasLoadError, value))
                OnPropertyChanged(nameof(EmptyMessage));
        }
    }

    public MainViewModel()
    {
    }

    partial void OnIsMonthlyExpensesLoadingChanged(bool value) =>
        OnPropertyChanged(nameof(IsNotMonthlyExpensesLoading));

    [RelayCommand]
    private async Task OpenExpenseStatisticsAsync()
    {
        if (Vehicles.Count == 0)
        {
            await LoadVehiclesAsync();
            if (Vehicles.Count == 0)
                return;
        }

        await Shell.Current.GoToAsync(
            nameof(ExpenseStatisticsPage),
            new Dictionary<string, object> { ["vehicles"] = Vehicles.ToList() });
    }

    [RelayCommand]
    private async Task OpenWeatherLocationPopupAsync()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            WeatherErrorMessage = "Konum seçme penceresi açılamadı.";
            HasWeatherError = true;
            return;
        }

        var searchText = await page.DisplayPromptAsync(
            "Konum seç",
            "Hava durumu için şehir adını gir.",
            "Ara",
            "Vazgeç",
            "Örn. İstanbul",
            maxLength: 80);

        if (string.IsNullOrWhiteSpace(searchText))
            return;

        LocationSearchText = searchText;
        LocationOptions.Clear();
        SelectedWeatherLocation = null;
        WeatherErrorMessage = null;
        HasWeatherError = false;
        await SearchLocationsAsync();

        if (HasWeatherError)
        {
            await page.DisplayAlertAsync("Konum bulunamadı", WeatherErrorMessage, "Kapat");
            return;
        }

        var options = LocationOptions.Select(location => location.DisplayName).ToArray();
        var selectedName = await page.DisplayActionSheetAsync(
            "Arama sonuçlarından konum seç",
            "Vazgeç",
            null,
            options);

        var selectedIndex = Array.IndexOf(options, selectedName);
        if (selectedIndex < 0)
            return;

        SelectedWeatherLocation = LocationOptions[selectedIndex];
        await SelectWeatherLocationAsync();
    }

    [RelayCommand]
    private async Task LoadWeatherAsync()
    {
        var latitude = Preferences.Get(WeatherLatitudeKey, double.NaN);
        var longitude = Preferences.Get(WeatherLongitudeKey, double.NaN);
        var name = Preferences.Get(WeatherLocationNameKey, string.Empty);

        if (double.IsNaN(latitude) || double.IsNaN(longitude) || string.IsNullOrWhiteSpace(name))
            return;

        await FetchWeatherAsync(new WeatherLocation
        {
            Name = name,
            Latitude = latitude,
            Longitude = longitude
        });
    }

    [RelayCommand]
    private async Task SearchLocationsAsync()
    {
        var searchText = LocationSearchText.Trim();
        if (searchText.Length < 2)
        {
            WeatherErrorMessage = "Şehir aramak için en az iki karakter gir.";
            HasWeatherError = true;
            return;
        }

        if (IsWeatherBusy)
            return;

        IsWeatherBusy = true;
        WeatherErrorMessage = null;
        HasWeatherError = false;
        LocationOptions.Clear();

        try
        {
            var uri = "https://geocoding-api.open-meteo.com/v1/search" +
                      $"?name={Uri.EscapeDataString(searchText)}&count=10&language=tr&format=json";
            using var response = await Client.GetAsync(uri);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GeocodingResponse>(JsonOptions)
                ?? throw new JsonException("The geocoding API returned an empty response.");

            foreach (var location in result.Results ?? [])
            {
                LocationOptions.Add(new WeatherLocation
                {
                    Name = location.Name,
                    AdministrativeArea = location.Admin1,
                    Country = location.Country,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude
                });
            }

            if (LocationOptions.Count == 0)
            {
                WeatherErrorMessage = "Bu şehir için sonuç bulunamadı. Başka bir adla tekrar ara.";
                HasWeatherError = true;
            }
            else
            {
                SelectedWeatherLocation = LocationOptions[0];
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            Debug.WriteLine($"Weather locations could not be searched: {ex}");
            WeatherErrorMessage = "Şehir araması yapılamadı. İnternet bağlantını kontrol edip tekrar dene.";
            HasWeatherError = true;
        }
        finally
        {
            IsWeatherBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectWeatherLocationAsync()
    {
        if (SelectedWeatherLocation is null)
        {
            WeatherErrorMessage = "Önce arama sonuçlarından bir şehir seç.";
            HasWeatherError = true;
            return;
        }

        Preferences.Set(WeatherLatitudeKey, SelectedWeatherLocation.Latitude);
        Preferences.Set(WeatherLongitudeKey, SelectedWeatherLocation.Longitude);
        Preferences.Set(WeatherLocationNameKey, SelectedWeatherLocation.DisplayName);

        await FetchWeatherAsync(SelectedWeatherLocation);
    }

    [RelayCommand]
    private async Task RefreshWeatherAsync() => await LoadWeatherAsync();

    private async Task FetchWeatherAsync(WeatherLocation location)
    {
        if (IsWeatherBusy)
            return;

        IsWeatherBusy = true;
        HasWeatherError = false;
        WeatherErrorMessage = null;
        WeatherLocationText = location.DisplayName;

        try
        {
            var latitude = location.Latitude.ToString(CultureInfo.InvariantCulture);
            var longitude = location.Longitude.ToString(CultureInfo.InvariantCulture);
            var uri = "https://api.open-meteo.com/v1/forecast" +
                      $"?latitude={latitude}&longitude={longitude}" +
                      "&current=temperature_2m,weather_code" +
                      "&daily=precipitation_probability_max,precipitation_sum" +
                      "&forecast_days=1&timezone=auto";
            using var response = await Client.GetAsync(uri);
            response.EnsureSuccessStatusCode();

            var forecast = await response.Content.ReadFromJsonAsync<WeatherForecastResponse>(JsonOptions)
                ?? throw new JsonException("The weather API returned an empty response.");

            if (forecast.Current is null
                || forecast.Daily?.PrecipitationProbabilityMax?.FirstOrDefault() is not int rainProbability
                || forecast.Daily.PrecipitationSum?.FirstOrDefault() is not double precipitation)
            {
                throw new JsonException("The weather API response is missing current or daily data.");
            }

            WeatherIcon = GetWeatherIcon(forecast.Current.WeatherCode);
            WeatherSummary = $"{location.Name} · {GetWeatherDescription(forecast.Current.WeatherCode)} · " +
                             $"{forecast.Current.Temperature2m:N0}°C";
            WeatherDetails = $"Bugün yağış olasılığı %{rainProbability} · " +
                             $"Beklenen yağış {precipitation:N1} mm";

            if (rainProbability <= 20 && precipitation <= 0.1)
            {
                CarWashAdvice = "Yıkamak için güzel bir gün ☀️";
                CarWashAdviceBackground = "#EAF0F7";
                CarWashAdviceColor = "#25436B";
            }
            else
            {
                CarWashAdvice = "Yağış bekleniyor, bugün yıkama 🌧️";
                CarWashAdviceBackground = "#FFF3E8";
                CarWashAdviceColor = "#9A4D00";
            }

            HasWeather = true;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            Debug.WriteLine($"Weather forecast could not be loaded: {ex}");
            WeatherErrorMessage = "Hava durumu alınamadı. Bağlantını kontrol edip yeniden deneyebilirsin.";
            HasWeatherError = true;
        }
        finally
        {
            IsWeatherBusy = false;
        }
    }

    private static string GetWeatherDescription(int code) => code switch
    {
        0 => "Açık",
        1 => "Çoğunlukla açık",
        2 => "Parçalı bulutlu",
        3 => "Kapalı",
        45 or 48 => "Sisli",
        51 or 53 or 55 or 56 or 57 => "Çisenti",
        61 or 63 or 65 or 66 or 67 => "Yağmurlu",
        71 or 73 or 75 or 77 => "Karlı",
        80 or 81 or 82 => "Sağanak yağışlı",
        85 or 86 => "Kar sağanaklı",
        95 or 96 or 99 => "Fırtınalı",
        _ => "Hava durumu"
    };

    private static string GetWeatherIcon(int code) => code switch
    {
        0 or 1 => "☀️",
        2 or 3 => "☁️",
        45 or 48 => "🌫️",
        51 or 53 or 55 or 56 or 57 => "🌦️",
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "🌧️",
        71 or 73 or 75 or 77 or 85 or 86 => "❄️",
        95 or 96 or 99 => "⛈️",
        _ => "🌤️"
    };

    public void SelectBrand(string? selectedBrand)
    {
        Brand = selectedBrand ?? string.Empty;
        OnPropertyChanged(nameof(HasSelectedBrand));
        OnPropertyChanged(nameof(IsCustomBrand));
        Model = string.Empty;
        Engine = string.Empty;
        CustomBrand = string.Empty;
        CustomModel = string.Empty;
        CustomEngine = string.Empty;

        var models = Brand != OtherOption && VehicleCatalog.TryGetValue(Brand, out var catalogModels)
            ? catalogModels.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            : Enumerable.Empty<string>();
        ModelOptions = new ObservableCollection<string>(models.Append(OtherOption));
        EngineOptions = new ObservableCollection<string>();
        OnPropertyChanged(nameof(HasEngineOptions));
    }

    public void SelectModel(string? selectedModel)
    {
        Model = selectedModel ?? string.Empty;
        Engine = string.Empty;
        CustomModel = string.Empty;
        CustomEngine = string.Empty;
        OnPropertyChanged(nameof(IsCustomModel));

        var engines = Model != OtherOption && VehicleCatalog.TryGetValue(Brand, out var models)
            && models.TryGetValue(Model, out var matchingEngines)
                ? matchingEngines
                : Enumerable.Empty<string>();
        EngineOptions = new ObservableCollection<string>(engines.Append(OtherOption));
        OnPropertyChanged(nameof(HasEngineOptions));
    }

    [RelayCommand]
    private async Task LoadVehiclesAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        ErrorMessage = null;
        HasLoadError = false;

        try
        {
            await RefreshVehiclesAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            Debug.WriteLine($"Vehicle list could not be loaded: {ex}");
            ErrorMessage = GetRequestErrorMessage(ex);
            HasLoadError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadMonthlyExpensesAsync()
    {
        if (IsMonthlyExpensesLoading)
            return;

        IsMonthlyExpensesLoading = true;
        HasMonthlyExpensesError = false;
        MonthlyExpensesErrorMessage = null;

        try
        {
            if (HasLoadError)
                throw new HttpRequestException("Vehicle list is not available.");

            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var nextMonth = monthStart.AddMonths(1);
            var vehicleRecords = await Task.WhenAll(Vehicles.Select(async vehicle =>
            {
                using var maintenanceResponse = await Client.GetAsync(
                    $"{ApiEndpoints.Maintenances}/vehicle/{vehicle.id}");
                maintenanceResponse.EnsureSuccessStatusCode();

                var maintenanceRecords = await maintenanceResponse.Content
                    .ReadFromJsonAsync<List<MaintenanceSummaryDto>>(JsonOptions)
                    ?? throw new JsonException($"Maintenance response for vehicle {vehicle.id} was empty.");

                using var fuelResponse = await Client.GetAsync(
                    $"{ApiEndpoints.FuelRecords}/vehicle/{vehicle.id}");
                fuelResponse.EnsureSuccessStatusCode();

                var fuelRecords = await fuelResponse.Content
                    .ReadFromJsonAsync<List<FuelSummaryDto>>(JsonOptions)
                    ?? throw new JsonException($"Fuel response for vehicle {vehicle.id} was empty.");

                var monthlyMaintenance = maintenanceRecords.Where(record =>
                    record.date >= monthStart && record.date < nextMonth);
                var monthlyFuel = fuelRecords.Where(record =>
                    record.date >= monthStart && record.date < nextMonth);
                return (Costs: monthlyMaintenance.Sum(record => record.cost) + monthlyFuel.Sum(record => record.cost),
                    Count: monthlyMaintenance.Count() + monthlyFuel.Count());
            }));

            MonthlyExpensesTotal = $"{vehicleRecords.Sum(records => records.Costs):N2} ₺";
            MonthlyExpensesDetails =
                $"{vehicleRecords.Sum(records => records.Count)} kayıt · {DateTime.Today.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("tr-TR"))}";
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            Debug.WriteLine($"Monthly expenses could not be loaded: {ex}");
            MonthlyExpensesTotal = "—";
            MonthlyExpensesErrorMessage =
                "Bu ayın harcamaları yüklenemedi. Yeniden denemek için dokun.";
            HasMonthlyExpensesError = true;
        }
        finally
        {
            IsMonthlyExpensesLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddVehicle))]
    private async Task AddVehicleAsync()
    {
        ErrorMessage = null;
        HasLoadError = false;

        var selectedBrand = IsCustomBrand ? CustomBrand.Trim() : Brand.Trim();
        var selectedModel = IsCustomModel ? CustomModel.Trim() : Model.Trim();
        var selectedEngine = IsCustomEngine ? CustomEngine.Trim() : Engine.Trim();
        if (string.IsNullOrWhiteSpace(selectedBrand)
            || string.IsNullOrWhiteSpace(selectedModel)
            || string.IsNullOrWhiteSpace(selectedEngine))
        {
            ErrorMessage = "Marka, model ve motor bilgilerini gir.";
            return;
        }

        if (!int.TryParse(Year, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedYear)
            || parsedYear < 1886
            || parsedYear > DateTime.Today.Year + 1)
        {
            ErrorMessage = $"Model yılını 1886 ile {DateTime.Today.Year + 1} arasında gir.";
            return;
        }

        if (!int.TryParse(CurrentKm, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedKm)
            || parsedKm < 0)
        {
            ErrorMessage = "Kilometreyi sıfır veya daha büyük bir tam sayı olarak gir.";
            return;
        }

        var newVehicle = new VehicleDto
        {
            userId = 1,
            brand = selectedBrand,
            model = selectedModel,
            engine = selectedEngine,
            year = parsedYear,
            currentKm = parsedKm
        };

        IsLoading = true;

        try
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(newVehicle, JsonOptions),
                Encoding.UTF8,
                "application/json");
            using var response = await Client.PostAsync(ApiEndpoints.Vehicles, content);
            response.EnsureSuccessStatusCode();

            Brand = string.Empty;
            Model = string.Empty;
            CustomBrand = string.Empty;
            CustomModel = string.Empty;
            CustomEngine = string.Empty;
            Year = string.Empty;
            Engine = string.Empty;
            CurrentKm = string.Empty;
            ModelOptions.Clear();
            EngineOptions.Clear();
            OnPropertyChanged(nameof(HasSelectedBrand));
            OnPropertyChanged(nameof(IsCustomBrand));
            OnPropertyChanged(nameof(IsNotCustomBrand));
            OnPropertyChanged(nameof(IsCustomModel));
            OnPropertyChanged(nameof(IsNotCustomModel));
            OnPropertyChanged(nameof(IsCustomEngine));
            OnPropertyChanged(nameof(IsNotCustomEngine));
            OnPropertyChanged(nameof(HasEngineOptions));

            try
            {
                await RefreshVehiclesAsync();
                await LoadMonthlyExpensesAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
            {
                Debug.WriteLine($"Vehicle was saved, but the list could not be refreshed: {ex}");
                ErrorMessage = "Araç kaydedildi ancak liste yenilenemedi. Yeniden yüklemeyi deneyebilirsin.";
                HasLoadError = true;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Debug.WriteLine($"Vehicle could not be saved: {ex}");
            ErrorMessage = GetRequestErrorMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanAddVehicle() => !IsLoading;

    [RelayCommand]
    private async Task DeleteVehicleAsync(VehicleDto vehicle)
    {
        if (vehicle is null || IsLoading)
            return;

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
            return;

        var answer = await page.DisplayAlertAsync(
            "Aracı sil",
            $"{vehicle.brand} {vehicle.model} garajdan silinsin mi?",
            "Sil",
            "Vazgeç");

        if (!answer)
            return;

        ErrorMessage = null;
        HasLoadError = false;
        IsLoading = true;

        try
        {
            using var response = await Client.DeleteAsync($"{ApiEndpoints.Vehicles}/{vehicle.id}");
            response.EnsureSuccessStatusCode();
            Vehicles.Remove(vehicle);
            _aiMechanicChats.Remove(vehicle.id);
            if (SelectedAiVehicle?.id == vehicle.id)
                SelectedAiVehicle = Vehicles.FirstOrDefault();
            SaveVehicleOrder();
            OnPropertyChanged(nameof(VehicleCount));
            await LoadMonthlyExpensesAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Debug.WriteLine($"Vehicle {vehicle.id} could not be deleted: {ex}");
            ErrorMessage = GetRequestErrorMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GoToDetailsAsync(VehicleDto vehicle)
    {
        if (vehicle is null)
            return;

        await Shell.Current.GoToAsync(
            nameof(VehicleDetailPage),
            new Dictionary<string, object> { ["vehicle"] = vehicle });
    }

    [RelayCommand]
    private void ToggleAiMechanic() => IsAiMechanicOpen = !IsAiMechanicOpen;

    [RelayCommand]
    private void CloseAiMechanic() => IsAiMechanicOpen = false;

    [RelayCommand]
    private void HideAiMechanicButton() => IsAiMechanicButtonVisible = false;

    [RelayCommand]
    private void RestoreAiMechanicButton() => IsAiMechanicButtonVisible = true;

    [RelayCommand]
    private async Task SendAiMechanicMessageAsync()
    {
        var question = AiMechanicQuestion.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            AiMechanicErrorMessage = "Önce bir soru yaz.";
            return;
        }

        if (question.Length > 1200)
        {
            AiMechanicErrorMessage = "Sorun en fazla 1200 karakter olabilir.";
            return;
        }

        if (SelectedAiVehicle is null)
        {
            AiMechanicErrorMessage = "Önce garajına bir araç ekle.";
            return;
        }

        if (IsAiMechanicBusy)
            return;

        IsAiMechanicBusy = true;
        AiMechanicErrorMessage = null;
        try
        {
            var request = new AiMechanicChatRequestDto
            {
                vehicleId = SelectedAiVehicle.id,
                question = question,
                history = AiMechanicMessages
                    .Where(message => message.Role is "user" or "assistant")
                    .Skip(1)
                    .TakeLast(6)
                    .Select(message => new AiMechanicChatTurnDto
                    {
                        role = message.Role,
                        content = message.Content.Length <= 1200
                            ? message.Content
                            : message.Content[..1200]
                    })
                    .ToList()
            };
            using var requestTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(50));
            using var response = await Client.PostAsJsonAsync(
                ApiEndpoints.AiMechanicChat,
                request,
                JsonOptions,
                requestTimeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                var problem = await response.Content.ReadFromJsonAsync<ApiProblemDto>(JsonOptions);
                var validationMessage = problem?.errors?
                    .SelectMany(error => error.Value)
                    .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));
                var detail = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "Seçilen araç bulunamadı. Garajı yenileyip tekrar dene."
                    : response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                        ? problem?.detail ?? "Gemini servisine ulaşılamadı. API anahtarını ve ücretsiz kota durumunu kontrol et."
                        : response.StatusCode == System.Net.HttpStatusCode.BadRequest
                            ? validationMessage ?? problem?.detail ?? problem?.title ?? "Soru veya sohbet geçmişi API doğrulamasından geçemedi."
                        : $"İstek tamamlanamadı (HTTP {(int)response.StatusCode}).";
                AiMechanicErrorMessage = detail;
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<AiMechanicChatResponseDto>(
                JsonOptions,
                requestTimeout.Token)
                ?? throw new JsonException("The AI mechanic API returned an empty response.");
            if (string.IsNullOrWhiteSpace(result.answer))
                throw new JsonException("The AI mechanic API returned an empty answer.");

            AiMechanicMessages.Add(new AiMechanicChatMessage("user", question));
            AiMechanicMessages.Add(new AiMechanicChatMessage(
                "assistant",
                result.answer.Trim(),
                result.disclaimer));
            while (AiMechanicMessages.Count > 13)
                AiMechanicMessages.RemoveAt(1);
            AiMechanicQuestion = string.Empty;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or OperationCanceledException)
        {
            Debug.WriteLine($"AI mechanic request failed: {exception}");
            AiMechanicErrorMessage = exception is OperationCanceledException
                ? "AI 50 saniye içinde yanıt vermedi. Bağlantıyı ve Gemini kotasını kontrol edip tekrar dene."
                : exception is JsonException
                    ? "AI yanıtı okunamadı. Tekrar deneyebilirsin."
                    : "API'ye bağlanılamadı. Backend'in çalıştığını kontrol et.";
        }
        finally
        {
            IsAiMechanicBusy = false;
        }
    }

    public void ReorderVehicle(int draggedVehicleId, int targetVehicleId)
    {
        var currentIndex = -1;
        var targetIndex = -1;
        for (var index = 0; index < Vehicles.Count; index++)
        {
            if (Vehicles[index].id == draggedVehicleId)
                currentIndex = index;
            if (Vehicles[index].id == targetVehicleId)
                targetIndex = index;
        }

        if (currentIndex < 0 || targetIndex < 0 || currentIndex == targetIndex)
            return;

        var vehicle = Vehicles[currentIndex];
        Vehicles.RemoveAt(currentIndex);
        if (currentIndex < targetIndex)
            targetIndex--;
        Vehicles.Insert(targetIndex, vehicle);
        SaveVehicleOrder();
    }

    private void SaveVehicleOrder()
    {
        Preferences.Default.Set(
            VehicleOrderPreferenceKey,
            JsonSerializer.Serialize(Vehicles.Select(vehicle => vehicle.id)));
    }

    private async Task RefreshVehiclesAsync()
    {
        using var response = await Client.GetAsync(ApiEndpoints.Vehicles);
        response.EnsureSuccessStatusCode();

        var loadedVehicles = await response.Content.ReadFromJsonAsync<List<VehicleDto>>(JsonOptions)
            ?? throw new JsonException("The vehicle API returned an empty response.");
        var savedOrder = LoadVehicleOrder();
        var orderById = savedOrder
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);
        var orderedVehicles = loadedVehicles
            .OrderBy(vehicle => orderById.TryGetValue(vehicle.id, out var index) ? index : int.MaxValue)
            .ThenBy(vehicle => vehicle.id)
            .ToList();

        var loadedVehicleIds = orderedVehicles.Select(vehicle => vehicle.id).ToHashSet();
        for (var index = Vehicles.Count - 1; index >= 0; index--)
        {
            if (!loadedVehicleIds.Contains(Vehicles[index].id))
                Vehicles.RemoveAt(index);
        }

        for (var targetIndex = 0; targetIndex < orderedVehicles.Count; targetIndex++)
        {
            var loadedVehicle = orderedVehicles[targetIndex];
            var currentIndex = -1;
            for (var index = targetIndex; index < Vehicles.Count; index++)
            {
                if (Vehicles[index].id == loadedVehicle.id)
                {
                    currentIndex = index;
                    break;
                }
            }

            if (currentIndex < 0)
            {
                Vehicles.Insert(targetIndex, loadedVehicle);
                continue;
            }

            if (currentIndex != targetIndex)
                Vehicles.Move(currentIndex, targetIndex);

            var currentVehicle = Vehicles[targetIndex];
            currentVehicle.brand = loadedVehicle.brand;
            currentVehicle.model = loadedVehicle.model;
            currentVehicle.engine = loadedVehicle.engine;
            currentVehicle.year = loadedVehicle.year;
            currentVehicle.currentKm = loadedVehicle.currentKm;
        }

        var selectedVehicleId = SelectedAiVehicle?.id;
        SelectedAiVehicle = Vehicles.FirstOrDefault(vehicle => vehicle.id == selectedVehicleId)
            ?? Vehicles.FirstOrDefault();

        SaveVehicleOrder();
        OnPropertyChanged(nameof(VehicleCount));
    }

    private static List<int> LoadVehicleOrder()
    {
        var serializedOrder = Preferences.Default.Get(VehicleOrderPreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(serializedOrder))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<int>>(serializedOrder)?.Distinct().ToList() ?? [];
        }
        catch (JsonException exception)
        {
            Debug.WriteLine($"Saved vehicle order could not be read: {exception}");
            return [];
        }
    }

    private static string GetRequestErrorMessage(Exception exception)
    {
        if (exception is TaskCanceledException)
            return "Sunucu yanıt vermedi. API'nin çalıştığını kontrol edip tekrar dene.";

        if (exception is HttpRequestException { StatusCode: not null } httpException)
            return $"Sunucu isteği kabul etmedi (HTTP {(int)httpException.StatusCode}).";

        if (exception is JsonException)
            return "Sunucudan gelen araç verileri okunamadı.";

        return "API'ye bağlanılamadı. Sunucunun çalıştığını ve adresinin doğru olduğunu kontrol et.";
    }
}

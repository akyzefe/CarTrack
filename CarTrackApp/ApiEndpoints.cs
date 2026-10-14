namespace CarTrackApp;

internal static class ApiEndpoints
{
#if ANDROID
    private const string BaseUrl = "http://10.0.2.2:5116/api";
#else
    private const string BaseUrl = "http://localhost:5116/api";
#endif

    public const string Vehicles = $"{BaseUrl}/vehicles";
    public const string Maintenances = $"{BaseUrl}/maintenances";
    public const string FuelRecords = $"{BaseUrl}/fuelrecords";
    public const string VehicleDocuments = $"{BaseUrl}/vehicle-documents";
    public const string AiMechanicChat = $"{BaseUrl}/ai-mechanic/chat";
}

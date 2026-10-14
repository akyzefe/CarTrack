namespace CarTrack.Api.Models;

/// <summary>The AI mechanic's response to a vehicle-related question.</summary>
public sealed record AiMechanicChatResponse(
    string Answer,
    string Disclaimer);

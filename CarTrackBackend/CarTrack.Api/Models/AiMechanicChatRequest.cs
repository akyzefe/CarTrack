using System.ComponentModel.DataAnnotations;

namespace CarTrack.Api.Models;

/// <summary>A question and recent conversation sent to the AI mechanic for a vehicle.</summary>
public sealed record AiMechanicChatRequest
{
    /// <summary>The vehicle whose details should inform the answer.</summary>
    [Range(1, int.MaxValue)]
    public required int VehicleId { get; init; }

    /// <summary>The latest user question.</summary>
    [Required, StringLength(1200, MinimumLength = 1)]
    public required string Question { get; init; }

    /// <summary>Recent chat turns, in chronological order.</summary>
    [MaxLength(10)]
    public IReadOnlyList<AiMechanicChatTurn> History { get; init; } = [];
}

/// <summary>A previous user or assistant message in an AI mechanic conversation.</summary>
public sealed record AiMechanicChatTurn
{
    /// <summary>The message author, either user or assistant.</summary>
    [Required, RegularExpression("^(user|assistant)$")]
    public required string Role { get; init; }

    /// <summary>The message body.</summary>
    [Required, StringLength(12000, MinimumLength = 1)]
    public required string Content { get; init; }
}

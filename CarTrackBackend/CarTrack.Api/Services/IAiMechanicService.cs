using CarTrack.Api.Models;

namespace CarTrack.Api.Services;

public interface IAiMechanicService
{
    Task<AiMechanicChatResponse?> AskAsync(
        AiMechanicChatRequest request,
        CancellationToken cancellationToken);
}

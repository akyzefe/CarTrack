using CarTrack.Api.Models;
using CarTrack.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CarTrack.Api.Controllers;

/// <summary>Provides vehicle-aware AI mechanic conversations.</summary>
[ApiController]
[Route("api/ai-mechanic")]
public sealed class AiMechanicController(
    IAiMechanicService aiMechanicService,
    ILogger<AiMechanicController> logger) : ControllerBase
{
    /// <summary>Answers a vehicle question using its details and recent maintenance history.</summary>
    [HttpPost("chat")]
    [ProducesResponseType<AiMechanicChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AiMechanicChatResponse>> Chat(
        [FromBody] AiMechanicChatRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await aiMechanicService.AskAsync(request, cancellationToken);
            return result is null
                ? NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Araç bulunamadı."
                })
                : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The Gemini API key is not configured.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Gemini yapılandırması eksik.",
                detail: "Backend'de Gemini API anahtarını yapılandırıp tekrar deneyin.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "The Gemini API request failed.");
            var (title, detail) = exception.StatusCode switch
            {
                System.Net.HttpStatusCode.TooManyRequests =>
                    ("Gemini ücretsiz kullanım sınırına ulaşıldı.",
                        "Biraz bekleyip tekrar deneyin veya Google AI Studio'dan kota durumunu kontrol edin."),
                System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                    ("Gemini API anahtarı kabul edilmedi.",
                        "Backend'deki Gemini API anahtarını ve Google AI Studio'daki API erişimini kontrol edin."),
                System.Net.HttpStatusCode.BadRequest =>
                    ("Gemini isteği kabul etmedi.",
                        "Model ayarını ve Gemini API erişimini kontrol edip tekrar deneyin."),
                _ =>
                    ("AI Mechanic şu anda yanıt veremiyor.",
                        "İnternet bağlantısını, Gemini API anahtarını ve ücretsiz kota durumunu kontrol edip tekrar deneyin.")
            };
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: title,
                detail: detail);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The Gemini API request timed out.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "AI Mechanic yanıt süresi doldu.",
                detail: "Biraz sonra tekrar deneyin.");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Gemini returned an invalid AI mechanic response.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "AI Mechanic geçerli bir yanıt döndürmedi.",
                detail: "Biraz sonra tekrar deneyin.");
        }
    }
}

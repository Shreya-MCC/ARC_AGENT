using Microsoft.AspNetCore.Mvc;
using ARC.Api.Auth;
using ARC.Api.DTOs;
using ARC.Api.Services;

namespace ARC.Api.Controllers;

[ApiController]
[Route("v1/chat")]
public sealed class ChatController : ControllerBase
{
    private readonly ChatOrchestrator _chat;
    private readonly ChatSessionStore _sessions;

    public ChatController(ChatOrchestrator chat, ChatSessionStore sessions)
    {
        _chat = chat;
        _sessions = sessions;
    }

    [HttpGet("sessions/current")]
    public async Task<IActionResult> GetCurrentSession(CancellationToken cancellationToken)
    {
        var actor = ArcActorHttp.GetRequired(HttpContext);
        var history = await _sessions.GetCurrentSessionAsync(actor, cancellationToken);
        return Ok(history);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(CancellationToken cancellationToken)
    {
        var actor = ArcActorHttp.GetRequired(HttpContext);
        var history = await _sessions.StartNewSessionAsync(actor, cancellationToken);
        return Ok(history);
    }

    [HttpPost("messages")]
    public async Task<IActionResult> PostMessage(
        [FromBody] ChatMessageRequest body,
        CancellationToken cancellationToken)
    {
        var actor = ArcActorHttp.GetRequired(HttpContext);
        var response = await _chat.HandleAsync(body, actor, cancellationToken);
        return Ok(response);
    }

    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<IActionResult> GetSessionHistory(
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return BadRequest(new { error = "sessionId is required." });

        var history = await _sessions.GetHistoryAsync(sessionId.Trim(), cancellationToken);
        return Ok(history);
    }
}

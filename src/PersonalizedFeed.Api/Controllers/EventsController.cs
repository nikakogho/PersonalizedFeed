using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using PersonalizedFeed.Api.Contracts;
using PersonalizedFeed.Api.Helpers;
using PersonalizedFeed.Api.Messaging;
using PersonalizedFeed.Domain.Events;

namespace PersonalizedFeed.Api.Controllers;

[ApiController]
[Route("v1/events")]
public class EventsController : ControllerBase
{
    private readonly IUserEventSink _eventSink;
    private readonly IDistributedCache _cache;

    public EventsController(IUserEventSink eventSink, IDistributedCache cache)
    {
        _eventSink = eventSink;
        _cache = cache;
    }

    [HttpPost("batch")]
    public async Task<IActionResult> PostBatch(
        [FromHeader(Name = "X-Tenant-Id")] string? tenantId,
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        [FromHeader(Name = "X-User")] string? userHash,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        [FromBody] UserEventBatchRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(userHash) ||
            string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var errorMessage = ErrorMessageHelpers.ConstructMissingHeadersMessage(tenantId, apiKey, userHash, idempotencyKey);

            return BadRequest(new
            {
                error = errorMessage
            });
        }

        var cacheKey = $"idempotency:{tenantId}:{idempotencyKey}";
        var cachedValue = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cachedValue))
        {
            return Accepted();
        }

        if (request.Events is null || request.Events.Count == 0)
        {
            return BadRequest(new { error = "Events collection must not be empty." });
        }

        var events = new List<UserEvent>(request.Events.Count);

        foreach (var e in request.Events)
        {
            if (!TryParseEventType(e.Type, out var type))
            {
                return BadRequest(new { error = $"Unsupported event type '{e.Type}'." });
            }

            events.Add(new UserEvent(
                TenantId: tenantId,
                UserHash: userHash,
                Type: type,
                VideoId: e.VideoId,
                Timestamp: e.Timestamp,
                WatchTimeMs: e.WatchTimeMs,
                FeedRequestId: e.FeedRequestId,
                RankPosition: e.RankPosition));
        }

        var batch = new UserEventBatch(
            TenantId: tenantId,
            UserHash: userHash,
            Events: events);

        await _eventSink.HandleAsync(batch, ct);

        await _cache.SetStringAsync(
            cacheKey,
            "processed",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
            ct);

        // Looks async even in local mode; Inline sink does it inline
        return Accepted();
    }

    private static bool TryParseEventType(string type, out UserEventType eventType)
    {
        switch (type.ToLowerInvariant())
        {
            case "video_view":
                eventType = UserEventType.VideoView;
                return true;
            case "like":
                eventType = UserEventType.Like;
                return true;
            case "skip":
                eventType = UserEventType.Skip;
                return true;
            case "share":
                eventType = UserEventType.Share;
                return true;
            default:
                eventType = UserEventType.VideoView;
                return false;
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using PersonalizedFeed.Api.Contracts;
using PersonalizedFeed.Api.Helpers;
using PersonalizedFeed.Domain.Services;
using PersonalizedFeed.Domain.Services.Models;

namespace PersonalizedFeed.Api.Controllers;

[ApiController]
[Route("v1/feed")]
public class FeedController : ControllerBase
{
    private readonly IFeedService _feedService;

    public FeedController(IFeedService feedService)
    {
        _feedService = feedService;
    }

    [HttpGet]
    public async Task<ActionResult<FeedResponse>> GetFeed(
        [FromHeader(Name = "X-Tenant-Id")] string? tenantId,
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        [FromHeader(Name = "X-User")] string? userHash,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(userHash))
        {
            var errorMessage = ErrorMessageHelpers.ConstructMissingHeadersMessage(tenantId, apiKey, userHash);

            return BadRequest(new
            {
                error = errorMessage
            });
        }

        var request = new FeedRequest(
            TenantId: tenantId,
            ApiKey: apiKey,
            UserHash: userHash,
            Limit: limit);

        FeedResult result;
        try
        {
            result = await _feedService.GetFeedAsync(request, ct);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }

        var response = new FeedResponse(
            Mode: result.Mode.ToString().ToLowerInvariant(),
            Items: result.Items
                .Select(item => new FeedItemResponse(
                    VideoId: item.Video.VideoId,
                    PlaybackUrl: item.Video.PlaybackUrl,
                    ThumbnailUrl: item.Video.ThumbnailUrl,
                    Title: item.Video.Title,
                    MainTag: item.Video.MainTag,
                    Tags: item.Video.Tags,
                    DurationSeconds: item.Video.DurationSeconds,
                    MaturityRating: item.Video.MaturityRating,
                    Score: item.Score,
                    Rank: item.Rank))
                .ToList(),
            NextCursor: result.NextCursor);

        return Ok(response);
    }
}

using PersonalizedFeed.Domain.Models;

namespace PersonalizedFeed.Domain.Services.Models;

public sealed record FeedItem(
    Video Video,
    double Score,
    int Rank);

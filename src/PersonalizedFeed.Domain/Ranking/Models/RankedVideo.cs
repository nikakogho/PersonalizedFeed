using PersonalizedFeed.Domain.Models;

namespace PersonalizedFeed.Domain.Ranking.Models;

public sealed record RankedVideo(Video Video, double Score);

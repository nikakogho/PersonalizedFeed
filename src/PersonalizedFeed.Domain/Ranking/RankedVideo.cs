using PersonalizedFeed.Domain.Models;

namespace PersonalizedFeed.Domain.Ranking;

public sealed record RankedVideo(Video Video, double Score);

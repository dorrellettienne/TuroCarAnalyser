namespace TuroCarAnalyser.Models;

public sealed class RankedInsight
{
    public required string GroupName { get; init; }

    public int ListingCount { get; init; }

    public decimal? AverageDailyPrice { get; init; }

    public double? AverageUtilization { get; init; }

    public decimal? AverageMonthlyRevenue { get; init; }

    public double? AverageTrips { get; init; }

    public double? AverageFavorites { get; init; }

    public double? AverageReviews { get; init; }

    public double Score { get; init; }

    public string Takeaway { get; init; } = string.Empty;
}

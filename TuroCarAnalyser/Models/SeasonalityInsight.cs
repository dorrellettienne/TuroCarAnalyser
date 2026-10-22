namespace TuroCarAnalyser.Models;

public sealed class SeasonalityInsight
{
    public required string Scope { get; init; }

    public required string GroupName { get; init; }

    public int ListingCount { get; init; }

    public decimal? AverageMonthlyRevenue { get; init; }

    public double? AverageUtilization { get; init; }

    public double SeasonalityScore { get; init; }

    public double TrendPercent { get; init; }

    public string TrendLabel { get; init; } = string.Empty;

    public string RevenueSparkline { get; init; } = string.Empty;

    public string Takeaway { get; init; } = string.Empty;
}

namespace TuroCarAnalyser.Models;

public sealed class MarketGapOpportunity
{
    public required string Market { get; init; }

    public required string VehicleType { get; init; }

    public int ListingCount { get; init; }

    public decimal? AverageDailyPrice { get; init; }

    public double? AverageUtilization { get; init; }

    public decimal? AverageMonthlyRevenue { get; init; }

    public double GapScore { get; init; }

    public string OpportunitySummary { get; init; } = string.Empty;
}

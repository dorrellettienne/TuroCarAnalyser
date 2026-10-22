namespace TuroCarAnalyser.Models;

public sealed class SummaryMetrics
{
    public int ListingCount { get; init; }

    public int MarketCount { get; init; }

    public int SourceCount { get; init; }

    public decimal? AverageDailyPrice { get; init; }

    public double? AverageUtilization { get; init; }

    public decimal? AverageMonthlyRevenue { get; init; }

    public string BestVehicleType { get; init; } = "Need data";

    public string CurrencySymbol { get; init; } = "$";
}

namespace TuroCarAnalyser.Models;

public sealed class ListingRecommendation
{
    public required TuroListing Listing { get; init; }

    public double RecommendationScore { get; init; }

    public string RecommendationBand { get; init; } = string.Empty;

    public string RecommendationReason { get; init; } = string.Empty;

    public string VehicleName => Listing.DisplayName;

    public string HostName => string.IsNullOrWhiteSpace(Listing.HostName) ? "Unknown host" : Listing.HostName;

    public string VehicleType => Listing.VehicleType;

    public string Market => Listing.Market;

    public string SourceName => Listing.SourceName;

    public decimal? DailyPrice => Listing.DailyPrice;

    public decimal? EstimatedMonthlyRevenue => Listing.EstimatedMonthlyRevenue ?? Listing.Last12MonthRevenue;

    public double? UtilizationRate => Listing.UtilizationRate;

    public int? Trips => Listing.Trips;

    public int? Reviews => Listing.Reviews;
}

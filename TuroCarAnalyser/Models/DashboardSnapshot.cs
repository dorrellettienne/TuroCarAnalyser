namespace TuroCarAnalyser.Models;

public sealed class DashboardSnapshot
{
    public SummaryMetrics Summary { get; init; } = new();

    public RecommendationSummary Recommendation { get; init; } = new();

    public IReadOnlyList<RankedInsight> TopModels { get; init; } = Array.Empty<RankedInsight>();

    public IReadOnlyList<RankedInsight> TopVehicleTypes { get; init; } = Array.Empty<RankedInsight>();

    public IReadOnlyList<RankedInsight> TopMarkets { get; init; } = Array.Empty<RankedInsight>();

    public IReadOnlyList<MarketGapOpportunity> MarketGaps { get; init; } = Array.Empty<MarketGapOpportunity>();

    public IReadOnlyList<RankedInsight> TopHosts { get; init; } = Array.Empty<RankedInsight>();

    public IReadOnlyList<SeasonalityInsight> SeasonalityInsights { get; init; } = Array.Empty<SeasonalityInsight>();

    public IReadOnlyList<TuroListing> Listings { get; init; } = Array.Empty<TuroListing>();

    public IReadOnlyList<ListingRecommendation> RecommendedListings { get; init; } = Array.Empty<ListingRecommendation>();

    public IReadOnlyList<string> NarrativeInsights { get; init; } = Array.Empty<string>();
}

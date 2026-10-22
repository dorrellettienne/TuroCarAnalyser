namespace TuroCarAnalyser.Models;

public sealed class RecommendationSummary
{
    public string RecommendedVehicleType { get; init; } = "Need data";

    public string RecommendedModel { get; init; } = "Need data";

    public string RecommendedMarket { get; init; } = "Need data";

    public string RecommendedCandidate { get; init; } = "Need data";

    public string RecommendedCandidateScoreText { get; init; } = "n/a";

    public string Headline { get; init; } = "Import data to generate a car recommendation.";

    public string RecommendationReason { get; init; } = "Recommendations will appear here once the app can compare segment strength and listing-level demand.";
}

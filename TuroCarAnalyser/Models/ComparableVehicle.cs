namespace TuroCarAnalyser.Models;

public sealed class ComparableVehicle
{
    public required string VehicleName { get; init; }

    public string Market { get; init; } = "Unknown market";

    public string City { get; init; } = string.Empty;

    public string VehicleType { get; init; } = "Unclassified";

    public string Trim { get; init; } = string.Empty;

    public string Transmission { get; init; } = string.Empty;

    public decimal? DailyPrice { get; init; }

    public double? UtilizationRate { get; init; }

    public decimal? EstimatedMonthlyRevenue { get; init; }

    public int? Trips { get; init; }

    public int? Reviews { get; init; }

    public double SimilarityScore { get; init; }

    public string SimilarityLabel { get; init; } = string.Empty;

    public string SourceName { get; init; } = string.Empty;
}

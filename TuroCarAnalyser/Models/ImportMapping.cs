namespace TuroCarAnalyser.Models;

public sealed class ImportMapping
{
    public string? TitleColumn { get; set; }

    public string? MakeColumn { get; set; }

    public string? ModelColumn { get; set; }

    public string? YearColumn { get; set; }

    public string? DailyPriceColumn { get; set; }

    public string? MonthlyRevenueColumn { get; set; }

    public string? UtilizationColumn { get; set; }

    public string? TripsColumn { get; set; }

    public string? RatingColumn { get; set; }

    public string? LocationColumn { get; set; }

    public Dictionary<string, string> RawMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

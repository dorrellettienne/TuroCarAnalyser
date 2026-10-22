namespace TuroCarAnalyser.Models;

public sealed class MonthlyRevenuePoint
{
    public required string Label { get; init; }

    public string MonthLabel => Label;

    public required DateTime Period { get; init; }

    public decimal Revenue { get; init; }
}

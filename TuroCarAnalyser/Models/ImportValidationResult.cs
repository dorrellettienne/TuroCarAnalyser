namespace TuroCarAnalyser.Models;

public sealed class ImportValidationResult
{
    public bool IsKnownExportFormat { get; init; }

    public bool CanImport { get; init; }

    public List<string> MissingCriticalColumns { get; init; } = [];

    public List<string> MissingOptionalColumns { get; init; } = [];

    public List<string> DetectedMonthlyRevenueColumns { get; init; } = [];

    public List<string> Notes { get; init; } = [];
}

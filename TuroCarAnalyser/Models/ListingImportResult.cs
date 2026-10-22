namespace TuroCarAnalyser.Models;

public sealed class ListingImportResult
{
    public IReadOnlyList<TuroListing> Listings { get; init; } = Array.Empty<TuroListing>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

using System.Collections.Generic;
using System.Linq;

namespace TuroCarAnalyser.Models;

public sealed class TuroListing
{
    public required string SourceName { get; init; }

    public required string SourceFile { get; init; }

    public required string ListingTitle { get; init; }

    public string Make { get; init; } = "Unknown";

    public string Model { get; init; } = "Unknown";

    public string Trim { get; init; } = string.Empty;

    public int? Year { get; init; }

    public string VehicleType { get; init; } = "Unclassified";

    public string Market { get; init; } = "Unknown market";

    public string City { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string Transmission { get; init; } = string.Empty;

    public decimal? DailyPrice { get; init; }

    public double? UtilizationRate { get; init; }

    public double? BookedDaysPerMonth { get; init; }

    public decimal? EstimatedMonthlyRevenue { get; init; }

    public decimal? PreviousYearRevenue { get; init; }

    public decimal? Last12MonthRevenue { get; init; }

    public decimal? MarketValue { get; init; }

    public double? ReturnOnInvestment { get; init; }

    public int? Trips { get; init; }

    public int? Favorites { get; init; }

    public double? Rating { get; init; }

    public int? Reviews { get; init; }

    public int? DaysOnTuro { get; init; }

    public DateTime? ListingDate { get; init; }

    public double? WeeklyDiscountRate { get; init; }

    public double? MonthlyDiscountRate { get; init; }

    public double? ProtectionPlanRate { get; init; }

    public string AirportDelivery { get; init; } = string.Empty;

    public string InstantBookLocations { get; init; } = string.Empty;

    public string Extras { get; init; } = string.Empty;

    public string Badges { get; init; } = string.Empty;

    public string VehicleId { get; init; } = string.Empty;

    public string ImageUrl { get; init; } = string.Empty;

    public string ListingUrl { get; init; } = string.Empty;

    public string HostId { get; init; } = string.Empty;

    public string HostName { get; init; } = string.Empty;

    public string HostStatus { get; init; } = string.Empty;

    public string CurrencySymbol { get; init; } = "$";

    public IReadOnlyList<MonthlyRevenuePoint> MonthlyRevenueHistory { get; init; } = Array.Empty<MonthlyRevenuePoint>();

    public string VehicleLabel
    {
        get
        {
            var parts = new List<string>();

            if (Year.HasValue)
            {
                parts.Add(Year.Value.ToString());
            }

            if (!IsUnknown(Make))
            {
                parts.Add(Make);
            }

            if (!IsUnknown(Model))
            {
                parts.Add(Model);
            }

            if (!string.IsNullOrWhiteSpace(Trim))
            {
                parts.Add(Trim);
            }

            return string.Join(" ", parts);
        }
    }

    public string DisplayName => !string.IsNullOrWhiteSpace(VehicleLabel) ? VehicleLabel : ListingTitle;

    public string ModelKey
    {
        get
        {
            var label = string.Join(
                " ",
                new[] { Make, Model }.Where(value => !IsUnknown(value)));

            return !string.IsNullOrWhiteSpace(label) ? label : DisplayName;
        }
    }

    public string HostLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(HostName))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(HostStatus)
                ? HostName
                : $"{HostName} ({HostStatus})";
        }
    }

    private static bool IsUnknown(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || value.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Unknown market", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Unclassified", StringComparison.OrdinalIgnoreCase);
    }
}

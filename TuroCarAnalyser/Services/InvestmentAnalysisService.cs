using TuroCarAnalyser.Models;

namespace TuroCarAnalyser.Services;

public sealed class InvestmentAnalysisService
{
    public RoiProjection CreateProjection(TuroListing? listing, RoiScenario scenario)
    {
        if (listing is null || !listing.EstimatedMonthlyRevenue.HasValue)
        {
            return new RoiProjection
            {
                VehicleName = listing?.DisplayName ?? "Choose a listing",
                Summary = "Select a listing with revenue data to see net cash flow, yield, and break-even estimates.",
                HasEstimate = false,
            };
        }

        var monthlyRevenue = listing.EstimatedMonthlyRevenue.Value;
        var feeRate = Math.Clamp(scenario.TuroFeePercent / 100d, 0d, 0.95d);
        var monthlyTuroFees = decimal.Round(monthlyRevenue * (decimal)feeRate, 0, MidpointRounding.AwayFromZero);
        var monthlyLoanPayment = CalculateMonthlyPayment(scenario.PrincipalToFinance, scenario.LoanAprPercent, scenario.LoanTermMonths);
        var monthlyOperatingCosts = scenario.MonthlyOperatingCosts;
        var netCashFlow = monthlyRevenue - monthlyTuroFees - monthlyLoanPayment - monthlyOperatingCosts;
        var annualNetCashFlow = netCashFlow * 12m;
        decimal? breakEvenRevenue = feeRate >= 1d
            ? null
            : decimal.Round((monthlyLoanPayment + monthlyOperatingCosts) / (decimal)(1d - feeRate), 0, MidpointRounding.AwayFromZero);

        double? breakEvenUtilization = null;
        if (breakEvenRevenue.HasValue
            && listing.UtilizationRate.HasValue
            && monthlyRevenue > 0m
            && listing.UtilizationRate.Value > 0d)
        {
            breakEvenUtilization = listing.UtilizationRate.Value * ((double)breakEvenRevenue.Value / (double)monthlyRevenue);
        }

        double? annualYield = scenario.PurchasePrice > 0m
            ? (double)(annualNetCashFlow / scenario.PurchasePrice)
            : null;

        double? cashOnCash = scenario.InitialCashRequired > 0m
            ? (double)(annualNetCashFlow / scenario.InitialCashRequired)
            : null;

        return new RoiProjection
        {
            VehicleName = listing.DisplayName,
            BasisMonthlyRevenue = monthlyRevenue,
            MonthlyTuroFees = monthlyTuroFees,
            MonthlyLoanPayment = monthlyLoanPayment,
            MonthlyOperatingCosts = monthlyOperatingCosts,
            MonthlyNetCashFlow = netCashFlow,
            AnnualNetCashFlow = annualNetCashFlow,
            BreakEvenMonthlyRevenue = breakEvenRevenue,
            BreakEvenUtilization = breakEvenUtilization,
            AnnualYieldOnPurchase = annualYield,
            CashOnCashReturn = cashOnCash,
            InitialCashRequired = scenario.InitialCashRequired,
            Summary = BuildProjectionSummary(netCashFlow, cashOnCash, breakEvenUtilization),
            HasEstimate = true,
        };
    }

    public IReadOnlyList<ComparableVehicle> FindComparables(TuroListing? selectedListing, IEnumerable<TuroListing> listings)
    {
        if (selectedListing is null)
        {
            return Array.Empty<ComparableVehicle>();
        }

        var candidates = listings
            .Where(listing => !ReferenceEquals(listing, selectedListing))
            .ToList();

        if (candidates.Count == 0)
        {
            return Array.Empty<ComparableVehicle>();
        }

        var sameMarketCount = candidates.Count(listing => listing.Market.Equals(selectedListing.Market, StringComparison.OrdinalIgnoreCase));
        if (sameMarketCount >= 3)
        {
            candidates = candidates
                .Where(listing => listing.Market.Equals(selectedListing.Market, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var sameCityCount = candidates.Count(listing =>
            !string.IsNullOrWhiteSpace(selectedListing.City)
            && listing.City.Equals(selectedListing.City, StringComparison.OrdinalIgnoreCase));

        if (sameCityCount >= 3)
        {
            candidates = candidates
                .Where(listing => listing.City.Equals(selectedListing.City, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return candidates
            .Select(listing =>
            {
                var similarityScore = CalculateSimilarityScore(selectedListing, listing);
                return new ComparableVehicle
                {
                    VehicleName = listing.DisplayName,
                    Market = listing.Market,
                    City = listing.City,
                    VehicleType = listing.VehicleType,
                    Trim = listing.Trim,
                    Transmission = listing.Transmission,
                    DailyPrice = listing.DailyPrice,
                    UtilizationRate = listing.UtilizationRate,
                    EstimatedMonthlyRevenue = listing.EstimatedMonthlyRevenue,
                    Trips = listing.Trips,
                    Reviews = listing.Reviews,
                    SimilarityScore = similarityScore,
                    SimilarityLabel = BuildSimilarityLabel(selectedListing, listing),
                    SourceName = listing.SourceName,
                };
            })
            .Where(listing => listing.SimilarityScore > 18d)
            .OrderByDescending(listing => listing.SimilarityScore)
            .ThenByDescending(listing => listing.EstimatedMonthlyRevenue ?? 0m)
            .ThenByDescending(listing => listing.UtilizationRate ?? 0d)
            .Take(10)
            .ToList();
    }

    private static decimal CalculateMonthlyPayment(decimal principal, double annualRatePercent, int termMonths)
    {
        if (principal <= 0m || termMonths <= 0)
        {
            return 0m;
        }

        var monthlyRate = annualRatePercent / 1200d;
        if (monthlyRate <= 0d)
        {
            return decimal.Round(principal / termMonths, 0, MidpointRounding.AwayFromZero);
        }

        var rateFactor = Math.Pow(1d + monthlyRate, termMonths);
        var payment = (double)principal * monthlyRate * rateFactor / (rateFactor - 1d);
        return decimal.Round((decimal)payment, 0, MidpointRounding.AwayFromZero);
    }

    private static double CalculateSimilarityScore(TuroListing selectedListing, TuroListing candidate)
    {
        var score = 0d;

        if (!string.IsNullOrWhiteSpace(selectedListing.City)
            && candidate.City.Equals(selectedListing.City, StringComparison.OrdinalIgnoreCase))
        {
            score += 16d;
        }

        if (candidate.Market.Equals(selectedListing.Market, StringComparison.OrdinalIgnoreCase))
        {
            score += 12d;
        }

        if (candidate.VehicleType.Equals(selectedListing.VehicleType, StringComparison.OrdinalIgnoreCase))
        {
            score += 16d;
        }

        if (candidate.Make.Equals(selectedListing.Make, StringComparison.OrdinalIgnoreCase))
        {
            score += 12d;
        }

        if (candidate.Model.Equals(selectedListing.Model, StringComparison.OrdinalIgnoreCase))
        {
            score += 18d;
        }

        if (!string.IsNullOrWhiteSpace(selectedListing.Trim)
            && candidate.Trim.Equals(selectedListing.Trim, StringComparison.OrdinalIgnoreCase))
        {
            score += 10d;
        }

        if (!string.IsNullOrWhiteSpace(selectedListing.Transmission)
            && candidate.Transmission.Equals(selectedListing.Transmission, StringComparison.OrdinalIgnoreCase))
        {
            score += 6d;
        }

        if (selectedListing.Year.HasValue && candidate.Year.HasValue)
        {
            var yearDifference = Math.Abs(selectedListing.Year.Value - candidate.Year.Value);
            score += Math.Max(0d, 8d - (yearDifference * 2d));
        }

        if (selectedListing.DailyPrice.HasValue && candidate.DailyPrice.HasValue && selectedListing.DailyPrice.Value > 0m)
        {
            var differenceRatio = Math.Abs((double)(selectedListing.DailyPrice.Value - candidate.DailyPrice.Value) / (double)selectedListing.DailyPrice.Value);
            score += Math.Max(0d, 10d - (differenceRatio * 20d));
        }

        if (selectedListing.EstimatedMonthlyRevenue.HasValue && candidate.EstimatedMonthlyRevenue.HasValue && selectedListing.EstimatedMonthlyRevenue.Value > 0m)
        {
            var differenceRatio = Math.Abs((double)(selectedListing.EstimatedMonthlyRevenue.Value - candidate.EstimatedMonthlyRevenue.Value) / (double)selectedListing.EstimatedMonthlyRevenue.Value);
            score += Math.Max(0d, 6d - (differenceRatio * 12d));
        }

        return Math.Round(score, 1);
    }

    private static string BuildProjectionSummary(decimal netCashFlow, double? cashOnCash, double? breakEvenUtilization)
    {
        if (netCashFlow > 300m && cashOnCash is > 0.20d)
        {
            return "Healthy monthly cash flow with a strong cash-on-cash profile under these assumptions.";
        }

        if (netCashFlow > 0m)
        {
            return "This scenario stays cash-flow positive, but the margin is worth pressure-testing against slower months.";
        }

        if (breakEvenUtilization is > 1d)
        {
            return "This setup needs more than full current utilization to break even, so the deal likely needs a lower buy price or lower costs.";
        }

        return "This scenario runs negative under the current assumptions. Try reducing the purchase price, loan burden, or monthly cost stack.";
    }

    private static string BuildSimilarityLabel(TuroListing selectedListing, TuroListing candidate)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(selectedListing.City)
            && candidate.City.Equals(selectedListing.City, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("same city");
        }

        if (candidate.Market.Equals(selectedListing.Market, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("same market");
        }

        if (candidate.VehicleType.Equals(selectedListing.VehicleType, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("same vehicle type");
        }

        if (candidate.Make.Equals(selectedListing.Make, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("same make");
        }

        if (candidate.Model.Equals(selectedListing.Model, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("same model");
        }

        if (!string.IsNullOrWhiteSpace(selectedListing.Trim)
            && candidate.Trim.Equals(selectedListing.Trim, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("same trim");
        }

        if (!string.IsNullOrWhiteSpace(selectedListing.Transmission)
            && candidate.Transmission.Equals(selectedListing.Transmission, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("same transmission");
        }

        return parts.Count == 0
            ? "closest visible match on price and performance"
            : string.Join(", ", parts);
    }
}

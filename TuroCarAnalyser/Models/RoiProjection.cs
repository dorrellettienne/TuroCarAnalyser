namespace TuroCarAnalyser.Models;

public sealed class RoiProjection
{
    public string VehicleName { get; init; } = "Choose a listing";

    public decimal? BasisMonthlyRevenue { get; init; }

    public decimal MonthlyTuroFees { get; init; }

    public decimal MonthlyLoanPayment { get; init; }

    public decimal MonthlyOperatingCosts { get; init; }

    public decimal MonthlyNetCashFlow { get; init; }

    public decimal AnnualNetCashFlow { get; init; }

    public decimal? BreakEvenMonthlyRevenue { get; init; }

    public double? BreakEvenUtilization { get; init; }

    public double? AnnualYieldOnPurchase { get; init; }

    public double? CashOnCashReturn { get; init; }

    public decimal InitialCashRequired { get; init; }

    public string Summary { get; init; } = string.Empty;

    public bool HasEstimate { get; init; }
}

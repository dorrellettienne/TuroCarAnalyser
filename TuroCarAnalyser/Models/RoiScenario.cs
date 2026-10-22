namespace TuroCarAnalyser.Models;

public sealed class RoiScenario
{
    public decimal PurchasePrice { get; init; }

    public decimal DownPayment { get; init; }

    public double LoanAprPercent { get; init; }

    public int LoanTermMonths { get; init; }

    public decimal InsurancePerMonth { get; init; }

    public decimal MaintenancePerMonth { get; init; }

    public decimal CleaningPerMonth { get; init; }

    public decimal OtherFixedCostsPerMonth { get; init; }

    public double TuroFeePercent { get; init; }

    public decimal PrincipalToFinance => Math.Max(0m, PurchasePrice - DownPayment);

    public decimal MonthlyOperatingCosts => InsurancePerMonth + MaintenancePerMonth + CleaningPerMonth + OtherFixedCostsPerMonth;

    public decimal InitialCashRequired => PrincipalToFinance > 0m ? DownPayment : PurchasePrice;
}

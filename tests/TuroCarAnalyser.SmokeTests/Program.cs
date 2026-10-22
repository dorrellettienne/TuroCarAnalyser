using Microsoft.Data.Sqlite;
using TuroCarAnalyser.Services;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    Console.WriteLine($"PASS: {message}");
}

var importer = new CsvImportService();
var demo = importer.LoadFiles(Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "SampleData"), "*.csv"));
Check(demo.Listings.Count == 16, "Both demo formats import all 16 rows");
Check(demo.Warnings.Count == 0, "Demo imports have no warnings");
var snapshot = new AnalysisEngine().CreateSnapshot(demo.Listings);
Check(snapshot.Summary.ListingCount == 16 && snapshot.TopModels.Count > 0,
    "Imported rows produce dashboard metrics and model rankings");

var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"TuroCarAnalyser-smoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryDirectory);
try
{
    var fixture = Path.Combine(temporaryDirectory, "synthetic-export.csv");
    // Deliberately omit most optional columns and use a future month to check dynamic detection.
    File.WriteAllText(fixture,
        "Make,Model,Year,Avg Monthly Revenue,Avg Daily Price,Utilization Rate,City,Transmission,Listing URL,Vehicle ID,Rating,Reviews,Jan 2030,Feb 2030\n" +
        "Toyota,Yaris,2020,\u00a3462,\u00a340,39.4%,Demo City,Automatic,https://example.com/listing,demo-1,5.0\u2605,436,\u00a3100,\n");
    var inspection = importer.InspectCsv(fixture);
    Check(inspection.IsKnownExportFormat && inspection.CanImport, "Critical-only profile remains importable");
    Check(inspection.MissingOptionalColumns.Contains("Trim"), "Missing optional fields are reported");
    Check(inspection.DetectedMonthlyRevenueColumns.Count == 2, "Monthly columns are discovered dynamically");
    var imported = importer.LoadFiles(new[] { fixture });
    Check(imported.Listings.Count == 1, "Known export produces a listing");
    var listing = imported.Listings[0];
    Check(listing.DailyPrice == 40m && listing.EstimatedMonthlyRevenue == 462m,
        "Currency fields retain numeric values");
    Check(Math.Abs(listing.UtilizationRate.GetValueOrDefault() - 0.394) < 0.000001,
        "Utilization percentage is normalized");
    Check(listing.Rating == 5d && listing.Reviews == 436, "Rating is distinct from review count");
    Check(listing.MonthlyRevenueHistory.Count == 1 && listing.MonthlyRevenueHistory[0].Revenue == 100m,
        "Blank monthly revenue is omitted rather than treated as zero");

    var database = new KnowledgeBaseService(Path.Combine(temporaryDirectory, "smoke.db"));
    database.UpsertListings(imported.Listings);
    database.UpsertListings(imported.Listings);
    var restored = database.LoadAllListings();
    Check(restored.Count == 1 && restored[0].VehicleId == "demo-1",
        "SQLite upsert preserves identity without duplicating the listing");
    Check(restored[0].MonthlyRevenueHistory.Single().Revenue == 100m,
        "Monthly history survives the SQLite round trip");
}
finally
{
    SqliteConnection.ClearAllPools();
    Directory.Delete(temporaryDirectory, recursive: true);
}
Console.WriteLine("All smoke checks passed.");

using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TuroCarAnalyser.Models;

namespace TuroCarAnalyser.Services;

public sealed class KnowledgeBaseService
{
    private const string DatabaseFileName = "knowledge-base.db";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _databasePath;

    public KnowledgeBaseService(string? databasePath = null)
    {
        var rootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TuroCarAnalyser");

        Directory.CreateDirectory(rootPath);
        _databasePath = databasePath ?? Path.Combine(rootPath, DatabaseFileName);
        EnsureDatabase();
    }

    public string DatabasePath => _databasePath;

    public IReadOnlyList<TuroListing> LoadAllListings()
    {
        if (!File.Exists(_databasePath))
        {
            return Array.Empty<TuroListing>();
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                SourceName,
                SourceFile,
                ListingTitle,
                Make,
                Model,
                Trim,
                Year,
                VehicleType,
                Market,
                City,
                State,
                Transmission,
                DailyPrice,
                UtilizationRate,
                BookedDaysPerMonth,
                EstimatedMonthlyRevenue,
                PreviousYearRevenue,
                Last12MonthRevenue,
                MarketValue,
                ReturnOnInvestment,
                Trips,
                Favorites,
                Rating,
                Reviews,
                DaysOnTuro,
                ListingDate,
                WeeklyDiscountRate,
                MonthlyDiscountRate,
                ProtectionPlanRate,
                AirportDelivery,
                InstantBookLocations,
                Extras,
                Badges,
                VehicleId,
                ImageUrl,
                ListingUrl,
                HostId,
                HostName,
                HostStatus,
                CurrencySymbol,
                MonthlyRevenueHistoryJson
            FROM ListingKnowledge
            ORDER BY LastSeenAtUtc DESC, ImportedAtUtc DESC;
            """;

        using var reader = command.ExecuteReader();
        var listings = new List<TuroListing>();

        while (reader.Read())
        {
            listings.Add(new TuroListing
            {
                SourceName = GetRequiredString(reader, "SourceName"),
                SourceFile = GetRequiredString(reader, "SourceFile"),
                ListingTitle = GetRequiredString(reader, "ListingTitle"),
                Make = GetString(reader, "Make", "Unknown"),
                Model = GetString(reader, "Model", "Unknown"),
                Trim = GetString(reader, "Trim"),
                Year = GetNullableInt(reader, "Year"),
                VehicleType = GetString(reader, "VehicleType", "Unclassified"),
                Market = GetString(reader, "Market", "Unknown market"),
                City = GetString(reader, "City"),
                State = GetString(reader, "State"),
                Transmission = GetString(reader, "Transmission"),
                DailyPrice = GetNullableDecimal(reader, "DailyPrice"),
                UtilizationRate = GetNullableDouble(reader, "UtilizationRate"),
                BookedDaysPerMonth = GetNullableDouble(reader, "BookedDaysPerMonth"),
                EstimatedMonthlyRevenue = GetNullableDecimal(reader, "EstimatedMonthlyRevenue"),
                PreviousYearRevenue = GetNullableDecimal(reader, "PreviousYearRevenue"),
                Last12MonthRevenue = GetNullableDecimal(reader, "Last12MonthRevenue"),
                MarketValue = GetNullableDecimal(reader, "MarketValue"),
                ReturnOnInvestment = GetNullableDouble(reader, "ReturnOnInvestment"),
                Trips = GetNullableInt(reader, "Trips"),
                Favorites = GetNullableInt(reader, "Favorites"),
                Rating = GetNullableDouble(reader, "Rating"),
                Reviews = GetNullableInt(reader, "Reviews"),
                DaysOnTuro = GetNullableInt(reader, "DaysOnTuro"),
                ListingDate = GetNullableDateTime(reader, "ListingDate"),
                WeeklyDiscountRate = GetNullableDouble(reader, "WeeklyDiscountRate"),
                MonthlyDiscountRate = GetNullableDouble(reader, "MonthlyDiscountRate"),
                ProtectionPlanRate = GetNullableDouble(reader, "ProtectionPlanRate"),
                AirportDelivery = GetString(reader, "AirportDelivery"),
                InstantBookLocations = GetString(reader, "InstantBookLocations"),
                Extras = GetString(reader, "Extras"),
                Badges = GetString(reader, "Badges"),
                VehicleId = GetString(reader, "VehicleId"),
                ImageUrl = GetString(reader, "ImageUrl"),
                ListingUrl = GetString(reader, "ListingUrl"),
                HostId = GetString(reader, "HostId"),
                HostName = GetString(reader, "HostName"),
                HostStatus = GetString(reader, "HostStatus"),
                CurrencySymbol = GetString(reader, "CurrencySymbol", "$"),
                MonthlyRevenueHistory = DeserializeHistory(GetString(reader, "MonthlyRevenueHistoryJson")),
            });
        }

        return listings;
    }

    public void UpsertListings(IEnumerable<TuroListing> listings)
    {
        var records = listings
            .Where(listing => !string.IsNullOrWhiteSpace(listing.DisplayName))
            .ToList();

        if (records.Count == 0)
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var listing in records)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO ListingKnowledge (
                    ListingFingerprint,
                    SourceName,
                    SourceFile,
                    ListingTitle,
                    Make,
                    Model,
                    Trim,
                    Year,
                    VehicleType,
                    Market,
                    City,
                    State,
                    Transmission,
                    DailyPrice,
                    UtilizationRate,
                    BookedDaysPerMonth,
                    EstimatedMonthlyRevenue,
                    PreviousYearRevenue,
                    Last12MonthRevenue,
                    MarketValue,
                    ReturnOnInvestment,
                    Trips,
                    Favorites,
                    Rating,
                    Reviews,
                    DaysOnTuro,
                    ListingDate,
                    WeeklyDiscountRate,
                    MonthlyDiscountRate,
                    ProtectionPlanRate,
                    AirportDelivery,
                    InstantBookLocations,
                    Extras,
                    Badges,
                    VehicleId,
                    ImageUrl,
                    ListingUrl,
                    HostId,
                    HostName,
                    HostStatus,
                    CurrencySymbol,
                    MonthlyRevenueHistoryJson,
                    ImportedAtUtc,
                    LastSeenAtUtc
                )
                VALUES (
                    $ListingFingerprint,
                    $SourceName,
                    $SourceFile,
                    $ListingTitle,
                    $Make,
                    $Model,
                    $Trim,
                    $Year,
                    $VehicleType,
                    $Market,
                    $City,
                    $State,
                    $Transmission,
                    $DailyPrice,
                    $UtilizationRate,
                    $BookedDaysPerMonth,
                    $EstimatedMonthlyRevenue,
                    $PreviousYearRevenue,
                    $Last12MonthRevenue,
                    $MarketValue,
                    $ReturnOnInvestment,
                    $Trips,
                    $Favorites,
                    $Rating,
                    $Reviews,
                    $DaysOnTuro,
                    $ListingDate,
                    $WeeklyDiscountRate,
                    $MonthlyDiscountRate,
                    $ProtectionPlanRate,
                    $AirportDelivery,
                    $InstantBookLocations,
                    $Extras,
                    $Badges,
                    $VehicleId,
                    $ImageUrl,
                    $ListingUrl,
                    $HostId,
                    $HostName,
                    $HostStatus,
                    $CurrencySymbol,
                    $MonthlyRevenueHistoryJson,
                    $ImportedAtUtc,
                    $LastSeenAtUtc
                )
                ON CONFLICT(ListingFingerprint) DO UPDATE SET
                    SourceName = excluded.SourceName,
                    SourceFile = excluded.SourceFile,
                    ListingTitle = excluded.ListingTitle,
                    Make = excluded.Make,
                    Model = excluded.Model,
                    Trim = excluded.Trim,
                    Year = excluded.Year,
                    VehicleType = excluded.VehicleType,
                    Market = excluded.Market,
                    City = excluded.City,
                    State = excluded.State,
                    Transmission = excluded.Transmission,
                    DailyPrice = excluded.DailyPrice,
                    UtilizationRate = excluded.UtilizationRate,
                    BookedDaysPerMonth = excluded.BookedDaysPerMonth,
                    EstimatedMonthlyRevenue = excluded.EstimatedMonthlyRevenue,
                    PreviousYearRevenue = excluded.PreviousYearRevenue,
                    Last12MonthRevenue = excluded.Last12MonthRevenue,
                    MarketValue = excluded.MarketValue,
                    ReturnOnInvestment = excluded.ReturnOnInvestment,
                    Trips = excluded.Trips,
                    Favorites = excluded.Favorites,
                    Rating = excluded.Rating,
                    Reviews = excluded.Reviews,
                    DaysOnTuro = excluded.DaysOnTuro,
                    ListingDate = excluded.ListingDate,
                    WeeklyDiscountRate = excluded.WeeklyDiscountRate,
                    MonthlyDiscountRate = excluded.MonthlyDiscountRate,
                    ProtectionPlanRate = excluded.ProtectionPlanRate,
                    AirportDelivery = excluded.AirportDelivery,
                    InstantBookLocations = excluded.InstantBookLocations,
                    Extras = excluded.Extras,
                    Badges = excluded.Badges,
                    VehicleId = excluded.VehicleId,
                    ImageUrl = excluded.ImageUrl,
                    ListingUrl = excluded.ListingUrl,
                    HostId = excluded.HostId,
                    HostName = excluded.HostName,
                    HostStatus = excluded.HostStatus,
                    CurrencySymbol = excluded.CurrencySymbol,
                    MonthlyRevenueHistoryJson = excluded.MonthlyRevenueHistoryJson,
                    LastSeenAtUtc = excluded.LastSeenAtUtc;
                """;

            var timestamp = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            command.Parameters.AddWithValue("$ListingFingerprint", BuildFingerprint(listing));
            command.Parameters.AddWithValue("$SourceName", listing.SourceName);
            command.Parameters.AddWithValue("$SourceFile", listing.SourceFile);
            command.Parameters.AddWithValue("$ListingTitle", listing.ListingTitle);
            command.Parameters.AddWithValue("$Make", listing.Make);
            command.Parameters.AddWithValue("$Model", listing.Model);
            command.Parameters.AddWithValue("$Trim", listing.Trim);
            command.Parameters.AddWithValue("$Year", ToDbValue(listing.Year));
            command.Parameters.AddWithValue("$VehicleType", listing.VehicleType);
            command.Parameters.AddWithValue("$Market", listing.Market);
            command.Parameters.AddWithValue("$City", listing.City);
            command.Parameters.AddWithValue("$State", listing.State);
            command.Parameters.AddWithValue("$Transmission", listing.Transmission);
            command.Parameters.AddWithValue("$DailyPrice", ToDbValue(listing.DailyPrice));
            command.Parameters.AddWithValue("$UtilizationRate", ToDbValue(listing.UtilizationRate));
            command.Parameters.AddWithValue("$BookedDaysPerMonth", ToDbValue(listing.BookedDaysPerMonth));
            command.Parameters.AddWithValue("$EstimatedMonthlyRevenue", ToDbValue(listing.EstimatedMonthlyRevenue));
            command.Parameters.AddWithValue("$PreviousYearRevenue", ToDbValue(listing.PreviousYearRevenue));
            command.Parameters.AddWithValue("$Last12MonthRevenue", ToDbValue(listing.Last12MonthRevenue));
            command.Parameters.AddWithValue("$MarketValue", ToDbValue(listing.MarketValue));
            command.Parameters.AddWithValue("$ReturnOnInvestment", ToDbValue(listing.ReturnOnInvestment));
            command.Parameters.AddWithValue("$Trips", ToDbValue(listing.Trips));
            command.Parameters.AddWithValue("$Favorites", ToDbValue(listing.Favorites));
            command.Parameters.AddWithValue("$Rating", ToDbValue(listing.Rating));
            command.Parameters.AddWithValue("$Reviews", ToDbValue(listing.Reviews));
            command.Parameters.AddWithValue("$DaysOnTuro", ToDbValue(listing.DaysOnTuro));
            command.Parameters.AddWithValue("$ListingDate", ToDbValue(listing.ListingDate));
            command.Parameters.AddWithValue("$WeeklyDiscountRate", ToDbValue(listing.WeeklyDiscountRate));
            command.Parameters.AddWithValue("$MonthlyDiscountRate", ToDbValue(listing.MonthlyDiscountRate));
            command.Parameters.AddWithValue("$ProtectionPlanRate", ToDbValue(listing.ProtectionPlanRate));
            command.Parameters.AddWithValue("$AirportDelivery", listing.AirportDelivery);
            command.Parameters.AddWithValue("$InstantBookLocations", listing.InstantBookLocations);
            command.Parameters.AddWithValue("$Extras", listing.Extras);
            command.Parameters.AddWithValue("$Badges", listing.Badges);
            command.Parameters.AddWithValue("$VehicleId", listing.VehicleId);
            command.Parameters.AddWithValue("$ImageUrl", listing.ImageUrl);
            command.Parameters.AddWithValue("$ListingUrl", listing.ListingUrl);
            command.Parameters.AddWithValue("$HostId", listing.HostId);
            command.Parameters.AddWithValue("$HostName", listing.HostName);
            command.Parameters.AddWithValue("$HostStatus", listing.HostStatus);
            command.Parameters.AddWithValue("$CurrencySymbol", listing.CurrencySymbol);
            command.Parameters.AddWithValue("$MonthlyRevenueHistoryJson", JsonSerializer.Serialize(listing.MonthlyRevenueHistory, JsonOptions));
            command.Parameters.AddWithValue("$ImportedAtUtc", timestamp);
            command.Parameters.AddWithValue("$LastSeenAtUtc", timestamp);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private void EnsureDatabase()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS ListingKnowledge (
                ListingFingerprint TEXT PRIMARY KEY,
                SourceName TEXT NOT NULL,
                SourceFile TEXT NOT NULL,
                ListingTitle TEXT NOT NULL,
                Make TEXT NOT NULL,
                Model TEXT NOT NULL,
                Trim TEXT NOT NULL,
                Year INTEGER NULL,
                VehicleType TEXT NOT NULL,
                Market TEXT NOT NULL,
                City TEXT NOT NULL,
                State TEXT NOT NULL,
                Transmission TEXT NOT NULL,
                DailyPrice REAL NULL,
                UtilizationRate REAL NULL,
                BookedDaysPerMonth REAL NULL,
                EstimatedMonthlyRevenue REAL NULL,
                PreviousYearRevenue REAL NULL,
                Last12MonthRevenue REAL NULL,
                MarketValue REAL NULL,
                ReturnOnInvestment REAL NULL,
                Trips INTEGER NULL,
                Favorites INTEGER NULL,
                Rating REAL NULL,
                Reviews INTEGER NULL,
                DaysOnTuro INTEGER NULL,
                ListingDate TEXT NULL,
                WeeklyDiscountRate REAL NULL,
                MonthlyDiscountRate REAL NULL,
                ProtectionPlanRate REAL NULL,
                AirportDelivery TEXT NOT NULL,
                InstantBookLocations TEXT NOT NULL,
                Extras TEXT NOT NULL,
                Badges TEXT NOT NULL,
                VehicleId TEXT NOT NULL,
                ImageUrl TEXT NOT NULL,
                ListingUrl TEXT NOT NULL,
                HostId TEXT NOT NULL,
                HostName TEXT NOT NULL,
                HostStatus TEXT NOT NULL,
                CurrencySymbol TEXT NOT NULL,
                MonthlyRevenueHistoryJson TEXT NOT NULL,
                ImportedAtUtc TEXT NOT NULL,
                LastSeenAtUtc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_ListingKnowledge_Market ON ListingKnowledge (Market);
            CREATE INDEX IF NOT EXISTS IX_ListingKnowledge_VehicleType ON ListingKnowledge (VehicleType);
            CREATE INDEX IF NOT EXISTS IX_ListingKnowledge_Model ON ListingKnowledge (Make, Model);
            CREATE INDEX IF NOT EXISTS IX_ListingKnowledge_LastSeenAtUtc ON ListingKnowledge (LastSeenAtUtc);
            """;

        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());

        connection.Open();
        return connection;
    }

    private static IReadOnlyList<MonthlyRevenuePoint> DeserializeHistory(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return Array.Empty<MonthlyRevenuePoint>();
        }

        var history = JsonSerializer.Deserialize<List<MonthlyRevenuePoint>>(rawJson, JsonOptions);
        return history ?? new List<MonthlyRevenuePoint>();
    }

    private static string BuildFingerprint(TuroListing listing)
    {
        var basis = !string.IsNullOrWhiteSpace(listing.VehicleId)
            ? $"vehicle|{listing.SourceName}|{listing.VehicleId}"
            : !string.IsNullOrWhiteSpace(listing.ListingUrl)
                ? $"url|{listing.SourceName}|{listing.ListingUrl.Trim().ToLowerInvariant()}"
                : $"fallback|{listing.SourceName}|{listing.Market}|{listing.City}|{listing.HostName}|{listing.DisplayName}|{listing.DailyPrice?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}";

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(basis)));
    }

    private static object ToDbValue(decimal? value) => value.HasValue ? (double)value.Value : DBNull.Value;

    private static object ToDbValue(double? value) => value.HasValue ? value.Value : DBNull.Value;

    private static object ToDbValue(int? value) => value.HasValue ? value.Value : DBNull.Value;

    private static object ToDbValue(DateTime? value) => value.HasValue
        ? value.Value.ToString("O", CultureInfo.InvariantCulture)
        : DBNull.Value;

    private static string GetRequiredString(SqliteDataReader reader, string columnName)
    {
        var value = GetString(reader, columnName);
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
    }

    private static string GetString(SqliteDataReader reader, string columnName, string fallback = "")
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? fallback : reader.GetString(ordinal);
    }

    private static int? GetNullableInt(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static double? GetNullableDouble(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
    }

    private static decimal? GetNullableDecimal(SqliteDataReader reader, string columnName)
    {
        var value = GetNullableDouble(reader, columnName);
        return value.HasValue ? decimal.Round((decimal)value.Value, 2, MidpointRounding.AwayFromZero) : null;
    }

    private static DateTime? GetNullableDateTime(SqliteDataReader reader, string columnName)
    {
        var rawValue = GetString(reader, columnName);
        return DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedValue)
            ? parsedValue
            : null;
    }
}

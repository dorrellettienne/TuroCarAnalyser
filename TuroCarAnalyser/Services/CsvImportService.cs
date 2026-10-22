using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
using TuroCarAnalyser.Models;

namespace TuroCarAnalyser.Services;

public sealed class CsvImportService
{
    private const string KnownExportFormatName = "RentScout/Raptor-style export";

    private static readonly Dictionary<string, string[]> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] =
        [
            "listingtitle",
            "listingname",
            "vehicle",
            "vehiclename",
            "car",
            "carname",
            "name",
            "title",
        ],
        ["make"] = ["make", "brand", "manufacturer"],
        ["model"] = ["model", "vehiclemodel"],
        ["year"] = ["year", "vehicleyear", "modelyear", "caryear"],
        ["vehicleType"] =
        [
            "vehicletype",
            "type",
            "bodytype",
            "bodystyle",
            "vehicleclass",
            "segment",
            "category",
            "class",
        ],
        ["market"] = ["market", "metro", "location", "region", "area"],
        ["city"] = ["city", "pickupcity"],
        ["state"] = ["state", "province", "pickupstate"],
        ["dailyPrice"] =
        [
            "dailyprice",
            "avgdailyprice",
            "averagedailyprice",
            "averageprice",
            "dayrate",
            "priceperday",
            "dailyrate",
            "tripprice",
            "price",
            "rate",
        ],
        ["utilization"] =
        [
            "utilization",
            "utilizationrate",
            "occupancy",
            "occupancyrate",
            "bookedpct",
            "bookedpercentage",
            "utilizationpercent",
            "occupiedrate",
        ],
        ["bookedDays"] =
        [
            "bookeddays",
            "daysbooked",
            "renteddays",
            "tripdays",
            "occupieddays",
            "reservationdays",
        ],
        ["revenue"] =
        [
            "revenue",
            "monthlyrevenue",
            "estimatedmonthlyrevenue",
            "estmonthlyrevenue",
            "monthlyearnings",
            "estimatedearnings",
            "earnings",
            "monthlyincome",
        ],
        ["trips"] = ["trips", "tripcount", "completedtrips", "totaltrips", "bookingcount", "reservationcount"],
        ["reviews"] = ["reviews", "reviewcount", "totalreviews", "ratingscount"],
    };

    private static readonly string[] PriceExclusions = ["purchase", "msrp", "cost", "acquisition", "depreciation"];
    private static readonly string[] RevenueExclusions = ["lifetime", "alltime", "historical", "total"];

    private static readonly string[] KnownExportCriticalHeaders =
    [
        "Make",
        "Model",
        "Year",
        "Avg Monthly Revenue",
        "Avg Daily Price",
        "Utilization Rate",
        "City",
        "Transmission",
        "Listing URL",
        "Vehicle ID",
    ];

    private static readonly string[] KnownExportOptionalHeaders =
    [
        "Type",
        "Trim",
        "Previous Year Revenue",
        "Last 12 Month Revenue",
        "ROI",
        "Revenue Settings",
        "Market Value",
        "Days on Turo",
        "Completed Trips",
        "Favorites",
        "Rating",
        "Reviews",
        "Instant Book Locations",
        "Host ID",
        "Host Name",
        "Host Status",
        "Protection Plan Rate",
        "Airport Delivery",
        "Extras",
        "Badges",
        "State",
        "Color",
        "Daily Distance",
        "Weekly Distance",
        "Monthly Distance",
        "Excess Fee",
        "Weekly Discount",
        "Monthly Discount",
        "Listing Date",
        "Image URL",
    ];

    private static readonly HashSet<string> KnownExportHeaders = new(
        KnownExportCriticalHeaders.Concat(KnownExportOptionalHeaders),
        StringComparer.OrdinalIgnoreCase);

    private static readonly string[] KnownMakes =
    [
        "Mercedes-Benz",
        "Land Rover",
        "Alfa Romeo",
        "Aston Martin",
        "Rolls-Royce",
        "Chevrolet",
        "Chrysler",
        "Cadillac",
        "Genesis",
        "Hyundai",
        "Mercedes",
        "Mitsubishi",
        "Volkswagen",
        "Subaru",
        "Toyota",
        "Tesla",
        "Porsche",
        "Ferrari",
        "Maserati",
        "Vauxhall",
        "Jeep",
        "Lexus",
        "Mazda",
        "Honda",
        "Nissan",
        "Skoda",
        "Peugeot",
        "Renault",
        "Citroen",
        "Kia",
        "Ford",
        "BMW",
        "Audi",
        "Volvo",
        "GMC",
        "Ram",
        "Buick",
        "Mini",
    ];

    public List<string> GetHeaders(string filePath)
    {
        var firstLine = File.ReadLines(filePath).FirstOrDefault() ?? string.Empty;
        var delimiter = InferDelimiter(firstLine);

        using var parser = new TextFieldParser(filePath)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true,
        };

        parser.SetDelimiters(delimiter.ToString());

        if (parser.EndOfData)
        {
            return [];
        }

        return (parser.ReadFields() ?? Array.Empty<string>())
            .Select(CleanText)
            .ToList();
    }

    public ImportValidationResult InspectCsv(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new ImportValidationResult
            {
                IsKnownExportFormat = false,
                CanImport = false,
                MissingCriticalColumns = ["File not found."],
            };
        }

        return InspectHeaders(GetHeaders(filePath));
    }

    public ImportMapping InferMapping(List<string> headers)
    {
        var inspection = InspectHeaders(headers);
        if (inspection.IsKnownExportFormat)
        {
            return BuildKnownExportMapping();
        }

        var usedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mapping = new ImportMapping
        {
            MakeColumn = FindHeaderByKeywords(headers, usedHeaders, "make"),
            ModelColumn = FindHeaderByKeywords(headers, usedHeaders, "model"),
            YearColumn = FindHeaderByKeywords(headers, usedHeaders, "year"),
            DailyPriceColumn = FindHeaderByKeywords(headers, usedHeaders, "price", "daily", "dayrate", "day_rate", "avgdaily", "perday"),
            MonthlyRevenueColumn = FindHeaderByKeywords(headers, usedHeaders, "revenue", "monthly", "earnings"),
            UtilizationColumn = FindHeaderByKeywords(headers, usedHeaders, "util", "occupancy"),
            TripsColumn = FindHeaderByKeywords(headers, usedHeaders, "trip"),
            RatingColumn = FindHeaderByKeywords(headers, usedHeaders, "rating", "review"),
            LocationColumn = FindHeaderByKeywords(headers, usedHeaders, "location", "city", "market"),
        };

        mapping.TitleColumn = FindHeaderByKeywords(headers, usedHeaders, "title", "name", "vehicle", "car")
            ?? headers.FirstOrDefault(header => !usedHeaders.Contains(header));

        mapping.RawMappings = BuildRawMappings(mapping);
        return mapping;
    }

    private static ImportValidationResult InspectHeaders(IReadOnlyList<string> headers)
    {
        var headerSet = headers
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingCritical = KnownExportCriticalHeaders
            .Where(header => !headerSet.Contains(header))
            .ToList();

        var missingOptional = KnownExportOptionalHeaders
            .Where(header => !headerSet.Contains(header))
            .ToList();

        var monthlyColumns = headers
            .Where(IsMonthlyRevenueHeader)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var matchedKnownHeaders = headers.Count(header => KnownExportHeaders.Contains(header) || IsMonthlyRevenueHeader(header));
        var isKnownExportFormat = missingCritical.Count == 0
            && matchedKnownHeaders >= KnownExportCriticalHeaders.Length;

        var notes = new List<string>();
        if (isKnownExportFormat)
        {
            notes.Add($"Detected format: {KnownExportFormatName}.");
        }
        else
        {
            notes.Add("Known export format was not detected. Falling back to heuristic mapping.");
        }

        if (monthlyColumns.Count > 0)
        {
            notes.Add($"Detected {monthlyColumns.Count:N0} monthly revenue column(s).");
        }

        return new ImportValidationResult
        {
            IsKnownExportFormat = isKnownExportFormat,
            CanImport = isKnownExportFormat && missingCritical.Count == 0,
            MissingCriticalColumns = missingCritical,
            MissingOptionalColumns = missingOptional,
            DetectedMonthlyRevenueColumns = monthlyColumns,
            Notes = notes,
        };
    }

    private static ImportMapping BuildKnownExportMapping()
    {
        var mapping = new ImportMapping
        {
            MakeColumn = "Make",
            ModelColumn = "Model",
            YearColumn = "Year",
            DailyPriceColumn = "Avg Daily Price",
            MonthlyRevenueColumn = "Avg Monthly Revenue",
            UtilizationColumn = "Utilization Rate",
            TripsColumn = "Completed Trips",
            RatingColumn = "Rating",
            LocationColumn = "City",
        };

        mapping.RawMappings = BuildRawMappings(mapping);
        return mapping;
    }

    public ListingImportResult LoadFiles(IEnumerable<string> filePaths)
    {
        var listings = new List<TuroListing>();
        var warnings = new List<string>();

        foreach (var filePath in filePaths
                     .Where(File.Exists)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var headers = GetHeaders(filePath);
                var inspection = InspectHeaders(headers);
                var mapping = inspection.IsKnownExportFormat
                    ? BuildKnownExportMapping()
                    : InferMapping(headers);

                listings.AddRange(LoadSingleFile(filePath, mapping, warnings));
            }
            catch (Exception ex)
            {
                warnings.Add($"{Path.GetFileName(filePath)} could not be read: {ex.Message}");
            }
        }

        return new ListingImportResult
        {
            Listings = listings,
            Warnings = warnings,
        };
    }

    public ListingImportResult LoadFiles(IEnumerable<(string FilePath, ImportMapping Mapping)> fileMappings)
    {
        var listings = new List<TuroListing>();
        var warnings = new List<string>();

        foreach (var (filePath, mapping) in fileMappings)
        {
            if (!File.Exists(filePath))
            {
                continue;
            }

            try
            {
                listings.AddRange(LoadSingleFile(filePath, mapping, warnings));
            }
            catch (Exception ex)
            {
                warnings.Add($"{Path.GetFileName(filePath)} could not be read: {ex.Message}");
            }
        }

        return new ListingImportResult
        {
            Listings = listings,
            Warnings = warnings,
        };
    }

    private static IReadOnlyList<TuroListing> LoadSingleFile(string filePath, ImportMapping mapping, List<string> warnings)
    {
        var importedListings = new List<TuroListing>();
        var firstLine = File.ReadLines(filePath).FirstOrDefault() ?? string.Empty;
        var delimiter = InferDelimiter(firstLine);

        using var parser = new TextFieldParser(filePath)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true,
        };

        parser.SetDelimiters(delimiter.ToString());

        if (parser.EndOfData)
        {
            warnings.Add($"{Path.GetFileName(filePath)} is empty.");
            return importedListings;
        }

        var rawHeaders = parser.ReadFields();
        if (rawHeaders is null || rawHeaders.Length == 0)
        {
            warnings.Add($"{Path.GetFileName(filePath)} does not include a readable header row.");
            return importedListings;
        }

        var headers = rawHeaders
            .Select(CleanText)
            .ToArray();

        var inspection = InspectHeaders(headers);
        var effectiveMapping = inspection.IsKnownExportFormat
            ? BuildKnownExportMapping()
            : mapping;

        effectiveMapping.RawMappings = BuildRawMappings(effectiveMapping);
        var headerLookup = BuildHeaderLookup(headers);
        var columnResolution = inspection.IsKnownExportFormat
            ? ResolveKnownExportColumns(headerLookup)
            : ResolveColumns(effectiveMapping, headers, headerLookup);
        var importedRowCount = 0;

        if (inspection.IsKnownExportFormat)
        {
            warnings.Add($"{Path.GetFileName(filePath)} detected as {KnownExportFormatName}.");

            if (inspection.DetectedMonthlyRevenueColumns.Count > 0)
            {
                warnings.Add($"{Path.GetFileName(filePath)} monthly columns detected: {string.Join(", ", inspection.DetectedMonthlyRevenueColumns)}.");
            }

            if (inspection.MissingOptionalColumns.Count > 0)
            {
                warnings.Add($"{Path.GetFileName(filePath)} is missing optional columns: {string.Join(", ", inspection.MissingOptionalColumns.Take(10))}{(inspection.MissingOptionalColumns.Count > 10 ? "..." : string.Empty)}");
            }
        }

        while (!parser.EndOfData)
        {
            string[]? fields;

            try
            {
                fields = parser.ReadFields();
            }
            catch (MalformedLineException)
            {
                warnings.Add($"{Path.GetFileName(filePath)} has a malformed CSV row near line {parser.ErrorLineNumber}.");
                continue;
            }

            if (fields is null || fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var listing = CreateListing(fields, columnResolution, headers, filePath);
            if (listing is null)
            {
                continue;
            }

            importedListings.Add(listing);
            importedRowCount++;
        }

        if (importedRowCount == 0)
        {
            warnings.Add($"{Path.GetFileName(filePath)} loaded, but no usable rows were found with the confirmed mapping.");
        }
        else if (inspection.IsKnownExportFormat)
        {
            warnings.Add($"{Path.GetFileName(filePath)} imported {importedRowCount:N0} row(s) from the {KnownExportFormatName} profile.");
        }

        return importedListings;
    }

    private static Dictionary<string, int> BuildHeaderLookup(IReadOnlyList<string> headers)
    {
        var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < headers.Count; index++)
        {
            var header = headers[index];
            if (!lookup.ContainsKey(header))
            {
                lookup[header] = index;
            }
        }

        return lookup;
    }

    private static ColumnResolution ResolveColumns(
        ImportMapping mapping,
        IReadOnlyList<string> headers,
        IReadOnlyDictionary<string, int> headerLookup)
    {
        return new ColumnResolution
        {
            TitleIndex = ResolveIndex(headerLookup, mapping.TitleColumn, "title", "name", "vehicle", "car"),
            MakeIndex = ResolveIndex(headerLookup, mapping.MakeColumn, "make"),
            ModelIndex = ResolveIndex(headerLookup, mapping.ModelColumn, "model"),
            YearIndex = ResolveIndex(headerLookup, mapping.YearColumn, "year"),
            DailyPriceIndex = ResolveIndex(headerLookup, mapping.DailyPriceColumn, "price", "daily", "dayrate", "day_rate", "avgdaily", "perday"),
            MonthlyRevenueIndex = ResolveIndex(headerLookup, mapping.MonthlyRevenueColumn, "revenue", "monthly", "earnings"),
            UtilizationIndex = ResolveIndex(headerLookup, mapping.UtilizationColumn, "util", "occupancy"),
            MarketValueIndex = ResolveIndex(headerLookup, null, "marketvalue", "vehiclevalue"),
            RoiIndex = ResolveIndex(headerLookup, null, "roi", "returnoninvestment"),
            TripsIndex = ResolveIndex(headerLookup, mapping.TripsColumn, "trip"),
            RatingIndex = ResolveIndex(headerLookup, mapping.RatingColumn, "rating"),
            LocationIndex = ResolveIndex(headerLookup, mapping.LocationColumn, "location", "market"),
            ReviewsIndex = ResolveIndex(headerLookup, null, "reviews", "reviewcount", "totalreviews"),
            VehicleTypeIndex = ResolveIndex(headerLookup, null, "vehicletype", "type", "bodystyle", "body"),
            TrimIndex = ResolveIndex(headerLookup, null, "trim", "variant"),
            CityIndex = ResolveIndex(headerLookup, null, "city"),
            StateIndex = ResolveIndex(headerLookup, null, "state", "province", "region"),
            FavoritesIndex = ResolveIndex(headerLookup, null, "favorites", "favourites", "wishlist"),
            PreviousYearRevenueIndex = ResolveIndex(headerLookup, null, "previousyearrevenue", "previousyear"),
            Last12MonthRevenueIndex = ResolveIndex(headerLookup, null, "last12monthrevenue", "last12month", "twelvemonthrevenue"),
            DaysOnTuroIndex = ResolveIndex(headerLookup, null, "daysonturo", "listeddays", "dayslive"),
            ListingDateIndex = ResolveIndex(headerLookup, null, "listingdate", "createddate", "startdate"),
            TransmissionIndex = ResolveIndex(headerLookup, null, "transmission"),
            ColorIndex = ResolveIndex(headerLookup, null, "color", "colour"),
            WeeklyDiscountIndex = ResolveIndex(headerLookup, null, "weeklydiscount"),
            MonthlyDiscountIndex = ResolveIndex(headerLookup, null, "monthlydiscount"),
            AirportDeliveryIndex = ResolveIndex(headerLookup, null, "airportdelivery", "airport"),
            InstantBookLocationsIndex = ResolveIndex(headerLookup, null, "instantbooklocations", "instantbook"),
            ExtrasIndex = ResolveIndex(headerLookup, null, "extras"),
            BadgesIndex = ResolveIndex(headerLookup, null, "badges", "features"),
            VehicleIdIndex = ResolveIndex(headerLookup, null, "vehicleid", "listingid"),
            ImageUrlIndex = ResolveIndex(headerLookup, null, "imageurl", "image"),
            ListingUrlIndex = ResolveIndex(headerLookup, null, "listingurl", "url"),
            HostIdIndex = ResolveIndex(headerLookup, null, "hostid"),
            HostNameIndex = ResolveIndex(headerLookup, null, "hostname", "host"),
            HostStatusIndex = ResolveIndex(headerLookup, null, "hoststatus"),
            ProtectionPlanRateIndex = ResolveIndex(headerLookup, null, "protectionplanrate", "protectionplan"),
            RevenueSettingsIndex = ResolveIndex(headerLookup, null, "revenuesettings"),
            RevenueHistoryColumns = ResolveRevenueHistoryColumns(headers, headerLookup),
        };
    }

    private static ColumnResolution ResolveKnownExportColumns(IReadOnlyDictionary<string, int> headerLookup)
    {
        return new ColumnResolution
        {
            MakeIndex = ResolveExactIndex(headerLookup, "Make"),
            ModelIndex = ResolveExactIndex(headerLookup, "Model"),
            TrimIndex = ResolveExactIndex(headerLookup, "Trim"),
            YearIndex = ResolveExactIndex(headerLookup, "Year"),
            VehicleTypeIndex = ResolveExactIndex(headerLookup, "Type"),
            DailyPriceIndex = ResolveExactIndex(headerLookup, "Avg Daily Price"),
            MonthlyRevenueIndex = ResolveExactIndex(headerLookup, "Avg Monthly Revenue"),
            PreviousYearRevenueIndex = ResolveExactIndex(headerLookup, "Previous Year Revenue"),
            Last12MonthRevenueIndex = ResolveExactIndex(headerLookup, "Last 12 Month Revenue"),
            RoiIndex = ResolveExactIndex(headerLookup, "ROI"),
            MarketValueIndex = ResolveExactIndex(headerLookup, "Market Value"),
            UtilizationIndex = ResolveExactIndex(headerLookup, "Utilization Rate"),
            TripsIndex = ResolveExactIndex(headerLookup, "Completed Trips"),
            FavoritesIndex = ResolveExactIndex(headerLookup, "Favorites"),
            RatingIndex = ResolveExactIndex(headerLookup, "Rating"),
            ReviewsIndex = ResolveExactIndex(headerLookup, "Reviews"),
            CityIndex = ResolveExactIndex(headerLookup, "City"),
            StateIndex = ResolveExactIndex(headerLookup, "State"),
            TransmissionIndex = ResolveExactIndex(headerLookup, "Transmission"),
            ColorIndex = ResolveExactIndex(headerLookup, "Color"),
            ListingDateIndex = ResolveExactIndex(headerLookup, "Listing Date"),
            VehicleIdIndex = ResolveExactIndex(headerLookup, "Vehicle ID"),
            ImageUrlIndex = ResolveExactIndex(headerLookup, "Image URL"),
            ListingUrlIndex = ResolveExactIndex(headerLookup, "Listing URL"),
            HostIdIndex = ResolveExactIndex(headerLookup, "Host ID"),
            HostNameIndex = ResolveExactIndex(headerLookup, "Host Name"),
            HostStatusIndex = ResolveExactIndex(headerLookup, "Host Status"),
            ProtectionPlanRateIndex = ResolveExactIndex(headerLookup, "Protection Plan Rate"),
            RevenueSettingsIndex = ResolveExactIndex(headerLookup, "Revenue Settings"),
            AirportDeliveryIndex = ResolveExactIndex(headerLookup, "Airport Delivery"),
            InstantBookLocationsIndex = ResolveExactIndex(headerLookup, "Instant Book Locations"),
            ExtrasIndex = ResolveExactIndex(headerLookup, "Extras"),
            BadgesIndex = ResolveExactIndex(headerLookup, "Badges"),
            RevenueHistoryColumns = ResolveRevenueHistoryColumns(headerLookup.Keys.ToList(), headerLookup),
        };
    }

    private static int? ResolveExactIndex(IReadOnlyDictionary<string, int> headerLookup, string headerName)
    {
        return headerLookup.TryGetValue(headerName, out var index) ? index : null;
    }

    private static int? ResolveIndex(
        IReadOnlyDictionary<string, int> headerLookup,
        string? explicitColumn,
        params string[] keywords)
    {
        if (!string.IsNullOrWhiteSpace(explicitColumn))
        {
            var cleanedExplicit = CleanText(explicitColumn);
            if (headerLookup.TryGetValue(cleanedExplicit, out var explicitIndex))
            {
                return explicitIndex;
            }
        }

        var match = headerLookup.Keys
            .Select(header => new
            {
                Header = header,
                Score = ScoreKeywordMatch(header, keywords),
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Header.Length)
            .FirstOrDefault();

        return match is null
            ? null
            : headerLookup[match.Header];
    }

    private static IReadOnlyList<RevenueHistoryColumn> ResolveRevenueHistoryColumns(
        IReadOnlyList<string> headers,
        IReadOnlyDictionary<string, int> headerLookup)
    {
        return headers
            .Where(IsMonthlyRevenueHeader)
            .Select(header => new RevenueHistoryColumn
            {
                Index = headerLookup[header],
                Period = DateTime.ParseExact(header, "MMM yyyy", CultureInfo.InvariantCulture),
                Label = header,
            })
            .OrderBy(column => column.Period)
            .ToList();
    }

    private static IReadOnlyList<MonthlyRevenuePoint> BuildMonthlyRevenueHistory(
        IReadOnlyList<string> fields,
        IReadOnlyList<RevenueHistoryColumn> columns)
    {
        var history = new List<MonthlyRevenuePoint>();

        foreach (var column in columns)
        {
            var revenue = ParseDecimal(GetValue(fields, column.Index));
            if (!revenue.HasValue)
            {
                continue;
            }

            history.Add(new MonthlyRevenuePoint
            {
                Label = column.Label,
                Period = column.Period,
                Revenue = revenue.Value,
            });
        }

        return history;
    }

    private static bool IsMonthlyRevenueHeader(string header)
    {
        return DateTime.TryParseExact(
            CleanText(header),
            "MMM yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }

    private static TuroListing? CreateListing(
        IReadOnlyList<string> fields,
        ColumnResolution columns,
        IReadOnlyList<string> headers,
        string filePath)
    {
        var title = CleanText(GetValue(fields, columns.TitleIndex));
        var make = CleanText(GetValue(fields, columns.MakeIndex));
        var model = CleanText(GetValue(fields, columns.ModelIndex));
        var trim = CleanText(GetValue(fields, columns.TrimIndex));
        var year = ParseYear(GetValue(fields, columns.YearIndex));

        var explicitMarket = CleanText(GetValue(fields, columns.LocationIndex));
        var city = CleanText(GetValue(fields, columns.CityIndex));
        var state = CleanText(GetValue(fields, columns.StateIndex));
        var market = BuildMarket(explicitMarket, city, state);

        var rawDailyPrice = GetValue(fields, columns.DailyPriceIndex);
        var rawMonthlyRevenue = GetValue(fields, columns.MonthlyRevenueIndex);
        var rawPreviousYearRevenue = GetValue(fields, columns.PreviousYearRevenueIndex);
        var rawLast12MonthRevenue = GetValue(fields, columns.Last12MonthRevenueIndex);
        var rawMarketValue = GetValue(fields, columns.MarketValueIndex);
        var rawRoi = GetValue(fields, columns.RoiIndex);
        var rawUtilization = GetValue(fields, columns.UtilizationIndex);
        var rawRating = GetValue(fields, columns.RatingIndex);
        var rawReviews = GetValue(fields, columns.ReviewsIndex);

        var dailyPrice = ParseDecimal(rawDailyPrice);
        var monthlyRevenue = ParseDecimal(rawMonthlyRevenue);
        var previousYearRevenue = ParseDecimal(rawPreviousYearRevenue);
        var last12MonthRevenue = ParseDecimal(rawLast12MonthRevenue);
        var marketValue = ParseDecimal(rawMarketValue);
        var roi = ParsePercentage(rawRoi);
        var utilization = ParseUtilization(rawUtilization);
        var rating = ParseRating(rawRating);
        var reviews = ParseInt(rawReviews);
        var vehicleType = CleanVehicleType(GetValue(fields, columns.VehicleTypeIndex));
        var revenueHistory = BuildMonthlyRevenueHistory(fields, columns.RevenueHistoryColumns);

        if (!reviews.HasValue && rating.HasValue && rating.Value > 5d)
        {
            reviews = (int)Math.Round(rating.Value, MidpointRounding.AwayFromZero);
            rating = null;
        }

        if (!last12MonthRevenue.HasValue && revenueHistory.Count > 0)
        {
            last12MonthRevenue = revenueHistory.Sum(point => point.Revenue);
        }

        if (!monthlyRevenue.HasValue && revenueHistory.Count > 0)
        {
            monthlyRevenue = decimal.Round(
                revenueHistory.Average(point => point.Revenue),
                0,
                MidpointRounding.AwayFromZero);
        }

        var inferred = InferVehicleDetails(title);

        year ??= inferred.Year;
        make = !string.IsNullOrWhiteSpace(make) ? make : inferred.Make;
        model = !string.IsNullOrWhiteSpace(model) ? model : inferred.Model;

        if (string.IsNullOrWhiteSpace(title))
        {
            title = ComposeTitle(year, make, model, trim);
        }

        if (string.IsNullOrWhiteSpace(vehicleType))
        {
            vehicleType = InferVehicleType(title);
        }

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(make) && string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        if (!dailyPrice.HasValue && !monthlyRevenue.HasValue && !last12MonthRevenue.HasValue)
        {
            return null;
        }

        if (!monthlyRevenue.HasValue && dailyPrice.HasValue && utilization.HasValue)
        {
            monthlyRevenue = Math.Round(dailyPrice.Value * (decimal)(utilization.Value * 30d), 2, MidpointRounding.AwayFromZero);
        }

        var sourceName = InferSourceName(filePath, headers);
        var currencySymbol = DetermineCurrencySymbol(
            rawDailyPrice,
            rawMonthlyRevenue,
            rawPreviousYearRevenue,
            rawLast12MonthRevenue,
            GetValue(fields, columns.AirportDeliveryIndex));

        return new TuroListing
        {
            SourceName = sourceName,
            SourceFile = filePath,
            ListingTitle = title,
            Make = !string.IsNullOrWhiteSpace(make) ? make : "Unknown",
            Model = !string.IsNullOrWhiteSpace(model) ? model : "Unknown",
            Trim = trim,
            Year = year,
            VehicleType = !string.IsNullOrWhiteSpace(vehicleType) ? vehicleType : "Unclassified",
            Market = !string.IsNullOrWhiteSpace(market) ? market : "Unknown market",
            City = city,
            State = state,
            Transmission = CleanText(GetValue(fields, columns.TransmissionIndex)),
            DailyPrice = dailyPrice,
            UtilizationRate = utilization,
            BookedDaysPerMonth = utilization.HasValue ? Math.Round(utilization.Value * 30d, 1) : null,
            EstimatedMonthlyRevenue = monthlyRevenue,
            PreviousYearRevenue = previousYearRevenue,
            Last12MonthRevenue = last12MonthRevenue,
            MarketValue = marketValue,
            ReturnOnInvestment = roi,
            Trips = ParseInt(GetValue(fields, columns.TripsIndex)),
            Favorites = ParseInt(GetValue(fields, columns.FavoritesIndex)),
            Rating = rating,
            Reviews = reviews,
            DaysOnTuro = ParseInt(GetValue(fields, columns.DaysOnTuroIndex)),
            ListingDate = ParseDate(GetValue(fields, columns.ListingDateIndex)),
            WeeklyDiscountRate = ParsePercentage(GetValue(fields, columns.WeeklyDiscountIndex)),
            MonthlyDiscountRate = ParsePercentage(GetValue(fields, columns.MonthlyDiscountIndex)),
            ProtectionPlanRate = ParsePercentage(GetValue(fields, columns.ProtectionPlanRateIndex)),
            AirportDelivery = CleanText(GetValue(fields, columns.AirportDeliveryIndex)),
            InstantBookLocations = CleanText(GetValue(fields, columns.InstantBookLocationsIndex)),
            Extras = CleanText(GetValue(fields, columns.ExtrasIndex)),
            Badges = CleanText(GetValue(fields, columns.BadgesIndex)),
            VehicleId = CleanText(GetValue(fields, columns.VehicleIdIndex)),
            ImageUrl = CleanText(GetValue(fields, columns.ImageUrlIndex)),
            ListingUrl = CleanText(GetValue(fields, columns.ListingUrlIndex)),
            HostId = CleanText(GetValue(fields, columns.HostIdIndex)),
            HostName = CleanText(GetValue(fields, columns.HostNameIndex)),
            HostStatus = CleanText(GetValue(fields, columns.HostStatusIndex)),
            CurrencySymbol = currencySymbol,
            MonthlyRevenueHistory = revenueHistory,
        };
    }

    private static string? FindHeaderByKeywords(
        IEnumerable<string> headers,
        HashSet<string> usedHeaders,
        params string[] keywords)
    {
        var match = headers
            .Where(header => !usedHeaders.Contains(header))
            .Select(header => new
            {
                Header = header,
                Score = ScoreKeywordMatch(header, keywords),
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .Select(candidate => candidate.Header)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(match))
        {
            usedHeaders.Add(match);
        }

        return match;
    }

    private static int ScoreKeywordMatch(string header, IReadOnlyList<string> keywords)
    {
        var normalizedHeader = NormalizeHeader(header);
        if (string.IsNullOrWhiteSpace(normalizedHeader))
        {
            return 0;
        }

        if (IsExcludedHeader(normalizedHeader, keywords))
        {
            return 0;
        }

        var bestScore = 0;

        for (var index = 0; index < keywords.Count; index++)
        {
            var keyword = NormalizeHeader(keywords[index]);
            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            var keywordScore = ScoreSingleKeywordMatch(header, normalizedHeader, keyword);
            if (keywordScore == 0)
            {
                continue;
            }

            var priorityBoost = (keywords.Count - index) * 5;
            bestScore = Math.Max(bestScore, keywordScore + priorityBoost);
        }

        return bestScore;
    }

    private static int ScoreSingleKeywordMatch(string header, string normalizedHeader, string keyword)
    {
        if (normalizedHeader.Equals(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        var headerTokens = GetHeaderTokens(header);
        if (headerTokens.Any(token => TokenMatchesKeyword(token, keyword)))
        {
            return 80;
        }

        if (keyword.Length >= 4
            && (normalizedHeader.StartsWith(keyword, StringComparison.OrdinalIgnoreCase)
                || normalizedHeader.EndsWith(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return 60;
        }

        if (keyword.Length >= 5
            && normalizedHeader.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 40;
        }

        return 0;
    }

    private static bool TokenMatchesKeyword(string token, string keyword)
    {
        if (token.Equals(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var singularToken = token.EndsWith('s') && token.Length > 1 ? token[..^1] : token;
        var singularKeyword = keyword.EndsWith('s') && keyword.Length > 1 ? keyword[..^1] : keyword;

        return singularToken.Equals(singularKeyword, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExcludedHeader(string normalizedHeader, IReadOnlyList<string> keywords)
    {
        var normalizedKeywords = keywords
            .Select(NormalizeHeader)
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .ToList();

        var isPriceMatch = normalizedKeywords.Any(keyword =>
            keyword.Contains("price", StringComparison.OrdinalIgnoreCase)
            || keyword.Contains("rate", StringComparison.OrdinalIgnoreCase)
            || keyword.Contains("perday", StringComparison.OrdinalIgnoreCase));

        if (isPriceMatch && PriceExclusions.Any(normalizedHeader.Contains))
        {
            return true;
        }

        var isRevenueMatch = normalizedKeywords.Any(keyword =>
            keyword.Contains("revenue", StringComparison.OrdinalIgnoreCase)
            || keyword.Contains("monthly", StringComparison.OrdinalIgnoreCase)
            || keyword.Contains("earnings", StringComparison.OrdinalIgnoreCase));

        return isRevenueMatch && RevenueExclusions.Any(normalizedHeader.Contains);
    }

    private static List<string> GetHeaderTokens(string header)
    {
        return Regex.Matches(header ?? string.Empty, @"[A-Za-z0-9]+")
            .Select(match => NormalizeHeader(match.Value))
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();
    }

    private static Dictionary<string, string> BuildRawMappings(ImportMapping mapping)
    {
        var rawMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddRawMapping(rawMappings, mapping.TitleColumn, "Title");
        AddRawMapping(rawMappings, mapping.MakeColumn, "Make");
        AddRawMapping(rawMappings, mapping.ModelColumn, "Model");
        AddRawMapping(rawMappings, mapping.YearColumn, "Year");
        AddRawMapping(rawMappings, mapping.DailyPriceColumn, "DailyPrice");
        AddRawMapping(rawMappings, mapping.MonthlyRevenueColumn, "MonthlyRevenue");
        AddRawMapping(rawMappings, mapping.UtilizationColumn, "Utilization");
        AddRawMapping(rawMappings, mapping.TripsColumn, "Trips");
        AddRawMapping(rawMappings, mapping.RatingColumn, "Rating");
        AddRawMapping(rawMappings, mapping.LocationColumn, "Location");
        return rawMappings;
    }

    private static void AddRawMapping(IDictionary<string, string> rawMappings, string? sourceColumn, string mappedField)
    {
        if (!string.IsNullOrWhiteSpace(sourceColumn))
        {
            rawMappings[sourceColumn] = mappedField;
        }
    }

    private static string GetValue(IReadOnlyList<string> fields, int? index)
    {
        return index.HasValue
            && index.Value >= 0
            && index.Value < fields.Count
            ? fields[index.Value]
            : string.Empty;
    }

    private static string InferSourceName(string filePath, IReadOnlyList<string> headers)
    {
        var filename = Path.GetFileNameWithoutExtension(filePath) ?? "Imported CSV";
        var normalizedName = NormalizeHeader(filename);
        var headerBlob = string.Join(" ", headers).ToLowerInvariant();

        if (normalizedName.Contains("raptor") || headerBlob.Contains("raptor"))
        {
            return "Raptor Explorer";
        }

        if (normalizedName.Contains("rentscout") || headerBlob.Contains("rentscout"))
        {
            return "RentScout";
        }

        return filename;
    }

    private static string BuildMarket(string market, string city, string state)
    {
        var explicitMarket = CleanText(market);
        var cleanCity = CleanText(city);
        var cleanState = CleanText(state);

        if (!string.IsNullOrWhiteSpace(explicitMarket))
        {
            if (!string.IsNullOrWhiteSpace(cleanState)
                && explicitMarket.Equals(cleanCity, StringComparison.OrdinalIgnoreCase))
            {
                return $"{explicitMarket}, {cleanState}";
            }

            return explicitMarket;
        }

        return (string.IsNullOrWhiteSpace(cleanCity), string.IsNullOrWhiteSpace(cleanState)) switch
        {
            (false, false) => $"{cleanCity}, {cleanState}",
            (false, true) => cleanCity,
            _ => "Unknown market",
        };
    }

    private static (int? Year, string Make, string Model) InferVehicleDetails(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return (null, string.Empty, string.Empty);
        }

        var normalizedTitle = Regex.Replace(title.Trim(), @"\s+", " ");
        var year = ParseYear(normalizedTitle);

        var cleanedTitle = normalizedTitle;
        if (year.HasValue)
        {
            cleanedTitle = Regex.Replace(cleanedTitle, @"^(19|20)\d{2}\s*", string.Empty).Trim();
        }

        foreach (var make in KnownMakes.OrderByDescending(value => value.Length))
        {
            if (!cleanedTitle.StartsWith(make, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var model = cleanedTitle[make.Length..].Trim(' ', '-', '/', '|');
            return (year, make, model);
        }

        var tokens = cleanedTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return (year, string.Empty, string.Empty);
        }

        var inferredMake = tokens[0];
        var inferredModel = tokens.Length > 1 ? string.Join(" ", tokens.Skip(1)) : string.Empty;
        return (year, inferredMake, inferredModel);
    }

    private static string InferVehicleType(string title)
    {
        var normalized = title.ToLowerInvariant();

        if (normalized.Contains("suv") || normalized.Contains("crossover"))
        {
            return "SUV / Crossover";
        }

        if (normalized.Contains("truck") || normalized.Contains("pickup"))
        {
            return "Truck";
        }

        if (normalized.Contains("minivan") || normalized.Contains("cargo van") || normalized.Contains("van"))
        {
            return "Van / Minivan";
        }

        if (normalized.Contains("convertible") || normalized.Contains("cabriolet") || normalized.Contains("roadster"))
        {
            return "Convertible";
        }

        if (normalized.Contains("coupe"))
        {
            return "Coupe";
        }

        if (normalized.Contains("wagon") || normalized.Contains("estate"))
        {
            return "Wagon";
        }

        if (normalized.Contains("sedan"))
        {
            return "Sedan";
        }

        if (normalized.Contains("electric") || normalized.Contains(" ev ") || normalized.EndsWith(" ev"))
        {
            return "EV";
        }

        return "Unclassified";
    }

    private static string ComposeTitle(int? year, string make, string model, string trim)
    {
        var parts = new[]
        {
            year?.ToString(CultureInfo.InvariantCulture),
            CleanText(make),
            CleanText(model),
            CleanText(trim),
        };

        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
    }

    private static char InferDelimiter(string firstLine)
    {
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return ',';
        }

        var commaCount = firstLine.Count(character => character == ',');
        var semicolonCount = firstLine.Count(character => character == ';');
        var tabCount = firstLine.Count(character => character == '\t');

        return new[]
            {
                (Delimiter: ',', Count: commaCount),
                (Delimiter: ';', Count: semicolonCount),
                (Delimiter: '\t', Count: tabCount),
            }
            .OrderByDescending(candidate => candidate.Count)
            .First().Delimiter;
    }

    private static string NormalizeHeader(string? header)
    {
        return string.Concat(CleanText(header).Where(char.IsLetterOrDigit)).ToLowerInvariant();
    }

    private static string CleanText(string? value)
    {
        var cleaned = NormalizeImportedText(value);
        return Regex.Replace(cleaned.Trim(), @"\s+", " ");
    }

    private static string NormalizeImportedText(string? value)
    {
        return FixEncodingArtifacts(value)
            .Replace("Â£", "£", StringComparison.OrdinalIgnoreCase)
            .Replace("Â€", "€", StringComparison.OrdinalIgnoreCase)
            .Replace("â˜…", "★", StringComparison.OrdinalIgnoreCase)
            .Replace("â­", "⭐", StringComparison.OrdinalIgnoreCase)
            .Replace("Ã‚Â£", "£", StringComparison.OrdinalIgnoreCase)
            .Replace("Ã‚â‚¬", "€", StringComparison.OrdinalIgnoreCase)
            .Replace("Ã¢Ëœâ€¦", "★", StringComparison.OrdinalIgnoreCase)
            .Replace("Ã¢Â­Â", "⭐", StringComparison.OrdinalIgnoreCase);
    }

    private static string FixEncodingArtifacts(string? value)
    {
        return (value ?? string.Empty)
            .Replace("Â£", "£", StringComparison.OrdinalIgnoreCase)
            .Replace("Â€", "€", StringComparison.OrdinalIgnoreCase)
            .Replace("â˜…", "★", StringComparison.OrdinalIgnoreCase)
            .Replace("â­", "⭐", StringComparison.OrdinalIgnoreCase)
            .Replace("Â", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("�", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanVehicleType(string? value)
    {
        var cleaned = CleanText(value);
        var normalized = NormalizeHeader(cleaned);

        return normalized switch
        {
            "car" or "cars" => "Car",
            "suv" or "suvs" => "SUV / Crossover",
            "minivan" or "minivans" => "Van / Minivan",
            "van" or "vans" or "cargovans" or "cargovan" => "Van / Minivan",
            _ => cleaned,
        };
    }

    private static decimal? ParseDecimal(string? rawValue)
    {
        var cleaned = CleanNumeric(rawValue);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        return decimal.TryParse(
            cleaned,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    private static double? ParseDouble(string? rawValue)
    {
        var cleaned = CleanNumeric(rawValue);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        return double.TryParse(
            cleaned,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    private static int? ParseInt(string? rawValue)
    {
        var parsed = ParseDouble(rawValue);
        return parsed.HasValue ? (int)Math.Round(parsed.Value, MidpointRounding.AwayFromZero) : null;
    }

    private static double? ParseUtilization(string? rawValue)
    {
        var parsed = ParseDouble(rawValue);
        if (!parsed.HasValue)
        {
            return null;
        }

        var value = parsed.Value;
        if ((rawValue ?? string.Empty).Contains('%') || value > 1d)
        {
            value /= 100d;
        }

        return Math.Clamp(value, 0d, 1d);
    }

    private static double? ParsePercentage(string? rawValue)
    {
        return ParseUtilization(rawValue);
    }

    private static double? ParseRating(string? rawValue)
    {
        var parsed = ParseDouble(rawValue);
        return parsed.HasValue ? Math.Round(parsed.Value, 2) : null;
    }

    private static int? ParseYear(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var parsed = ParseInt(rawValue);
        if (parsed is >= 1980 and <= 2100)
        {
            return parsed;
        }

        var match = Regex.Match(rawValue, @"(19|20)\d{2}");
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Value, CultureInfo.InvariantCulture, out var year)
            ? year
            : null;
    }

    private static DateTime? ParseDate(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var cleaned = CleanText(rawValue);
        var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "M/d/yyyy", "yyyy-MM-dd" };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(
                    cleaned,
                    format,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                return parsed;
            }
        }

        return DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallback)
            ? fallback
            : null;
    }

    private static string CleanNumeric(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var normalized = NormalizeImportedText(rawValue)
            .Replace("$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("USD", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("£", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("EUR", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("€", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(",", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("%", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return Regex.Replace(normalized, @"[^0-9\.\-]", string.Empty);
    }

    private static string DetermineCurrencySymbol(params string[] rawValues)
    {
        var rawBlob = NormalizeImportedText(string.Join(" ", rawValues.Where(value => !string.IsNullOrWhiteSpace(value))));
        if (rawBlob.Contains("£", StringComparison.OrdinalIgnoreCase)
            || rawBlob.Contains("Â£", StringComparison.OrdinalIgnoreCase))
        {
            return "£";
        }

        if (rawBlob.Contains("€", StringComparison.OrdinalIgnoreCase))
        {
            return "€";
        }

        return "$";
    }

    private sealed class ColumnResolution
    {
        public int? TitleIndex { get; init; }

        public int? MakeIndex { get; init; }

        public int? ModelIndex { get; init; }

        public int? TrimIndex { get; init; }

        public int? YearIndex { get; init; }

        public int? VehicleTypeIndex { get; init; }

        public int? LocationIndex { get; init; }

        public int? CityIndex { get; init; }

        public int? StateIndex { get; init; }

        public int? DailyPriceIndex { get; init; }

        public int? MonthlyRevenueIndex { get; init; }

        public int? PreviousYearRevenueIndex { get; init; }

        public int? Last12MonthRevenueIndex { get; init; }

        public int? MarketValueIndex { get; init; }

        public int? RoiIndex { get; init; }

        public int? UtilizationIndex { get; init; }

        public int? TripsIndex { get; init; }

        public int? FavoritesIndex { get; init; }

        public int? RatingIndex { get; init; }

        public int? ReviewsIndex { get; init; }

        public int? DaysOnTuroIndex { get; init; }

        public int? ListingDateIndex { get; init; }

        public int? TransmissionIndex { get; init; }

        public int? ColorIndex { get; init; }

        public int? WeeklyDiscountIndex { get; init; }

        public int? MonthlyDiscountIndex { get; init; }

        public int? AirportDeliveryIndex { get; init; }

        public int? InstantBookLocationsIndex { get; init; }

        public int? ExtrasIndex { get; init; }

        public int? BadgesIndex { get; init; }

        public int? VehicleIdIndex { get; init; }

        public int? ImageUrlIndex { get; init; }

        public int? ListingUrlIndex { get; init; }

        public int? HostIdIndex { get; init; }

        public int? HostNameIndex { get; init; }

        public int? HostStatusIndex { get; init; }

        public int? ProtectionPlanRateIndex { get; init; }

        public int? RevenueSettingsIndex { get; init; }

        public IReadOnlyList<RevenueHistoryColumn> RevenueHistoryColumns { get; init; } = Array.Empty<RevenueHistoryColumn>();
    }

    private sealed class RevenueHistoryColumn
    {
        public required int Index { get; init; }

        public required DateTime Period { get; init; }

        public required string Label { get; init; }
    }
}

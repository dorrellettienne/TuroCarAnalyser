using System.Globalization;
using TuroCarAnalyser.Models;

namespace TuroCarAnalyser.Services;

public sealed class AnalysisEngine
{
    public DashboardSnapshot CreateSnapshot(IEnumerable<TuroListing> listings, IEnumerable<TuroListing>? referenceListings = null)
    {
        var normalizedListings = listings
            .Where(listing => !string.IsNullOrWhiteSpace(listing.DisplayName))
            .OrderByDescending(listing => listing.EstimatedMonthlyRevenue ?? listing.Last12MonthRevenue ?? 0m)
            .ThenByDescending(listing => listing.UtilizationRate ?? 0d)
            .ToList();

        var normalizedReferenceListings = (referenceListings ?? listings)
            .Where(listing => !string.IsNullOrWhiteSpace(listing.DisplayName))
            .OrderByDescending(listing => listing.EstimatedMonthlyRevenue ?? listing.Last12MonthRevenue ?? 0m)
            .ThenByDescending(listing => listing.UtilizationRate ?? 0d)
            .ToList();

        if (normalizedListings.Count == 0)
        {
            return new DashboardSnapshot
            {
                NarrativeInsights =
                [
                    "Import one or more CSV exports to see which models, hosts, and vehicle types are outperforming the rest of the market.",
                    "Richer exports can now surface trend, seasonality, delivery setup, discounts, and host-level signals alongside the core pricing and utilization view.",
                ],
            };
        }

        if (normalizedReferenceListings.Count == 0)
        {
            normalizedReferenceListings = normalizedListings;
        }

        var topVehicleTypes = BuildInsights(
            normalizedListings,
            listing => listing.VehicleType,
            fallbackName: "Unclassified",
            minimumGroupSize: 1);

        var topModels = BuildInsights(
            normalizedListings,
            listing => listing.ModelKey,
            fallbackName: "Unknown model",
            minimumGroupSize: normalizedListings.Count >= 12 ? 2 : 1);

        var topMarkets = BuildInsights(
            normalizedListings,
            listing => listing.Market,
            fallbackName: "Unknown market",
            minimumGroupSize: 1);

        var topHosts = BuildHostInsights(normalizedListings);
        var marketGaps = BuildMarketGaps(normalizedListings);
        var seasonalityInsights = BuildSeasonalityInsights(normalizedListings);
        var referenceVehicleTypes = BuildInsights(
            normalizedReferenceListings,
            listing => listing.VehicleType,
            fallbackName: "Unclassified",
            minimumGroupSize: 1);

        var referenceModels = BuildInsights(
            normalizedReferenceListings,
            listing => listing.ModelKey,
            fallbackName: "Unknown model",
            minimumGroupSize: normalizedReferenceListings.Count >= 12 ? 2 : 1);

        var referenceMarkets = BuildInsights(
            normalizedReferenceListings,
            listing => listing.Market,
            fallbackName: "Unknown market",
            minimumGroupSize: 1);

        var referenceMarketGaps = BuildMarketGaps(normalizedReferenceListings);
        var recommendedListings = BuildListingRecommendations(normalizedListings, referenceVehicleTypes, referenceModels, referenceMarkets, referenceMarketGaps);
        var recommendation = BuildRecommendationSummary(referenceVehicleTypes, referenceModels, referenceMarkets, recommendedListings);
        var currencySymbol = DetermineCurrencySymbol(normalizedListings);

        var summary = new SummaryMetrics
        {
            ListingCount = normalizedListings.Count,
            MarketCount = normalizedListings
                .Select(listing => listing.Market)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            SourceCount = normalizedListings
                .Select(listing => listing.SourceName)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            AverageDailyPrice = AverageDecimal(normalizedListings.Select(listing => listing.DailyPrice)),
            AverageUtilization = AverageDouble(normalizedListings.Select(listing => listing.UtilizationRate)),
            AverageMonthlyRevenue = AverageDecimal(normalizedListings.Select(listing => listing.EstimatedMonthlyRevenue)),
            BestVehicleType = topVehicleTypes.FirstOrDefault()?.GroupName ?? "Need more data",
            CurrencySymbol = currencySymbol,
        };

        return new DashboardSnapshot
        {
            Summary = summary,
            Recommendation = recommendation,
            TopModels = topModels.Take(15).ToList(),
            TopVehicleTypes = topVehicleTypes.Take(15).ToList(),
            TopMarkets = topMarkets.Take(15).ToList(),
            MarketGaps = marketGaps.Take(15).ToList(),
            TopHosts = topHosts.Take(15).ToList(),
            SeasonalityInsights = seasonalityInsights.Take(18).ToList(),
            Listings = BuildVisibleListings(normalizedListings, recommendedListings),
            RecommendedListings = recommendedListings.Take(20).ToList(),
            NarrativeInsights = BuildNarrativeInsights(summary, topVehicleTypes, topModels, topMarkets, topHosts, seasonalityInsights, recommendation),
        };
    }

    private static IReadOnlyList<RankedInsight> BuildInsights(
        IReadOnlyList<TuroListing> listings,
        Func<TuroListing, string> groupingSelector,
        string fallbackName,
        int minimumGroupSize)
    {
        var groupedStats = listings
            .GroupBy(listing =>
            {
                var rawName = groupingSelector(listing)?.Trim();
                return string.IsNullOrWhiteSpace(rawName) ? fallbackName : rawName;
            })
            .Select(group => new GroupStats
            {
                GroupName = group.Key,
                ListingCount = group.Count(),
                AverageDailyPrice = AverageDecimal(group.Select(listing => listing.DailyPrice)),
                AverageUtilization = AverageDouble(group.Select(listing => listing.UtilizationRate)),
                AverageMonthlyRevenue = AverageDecimal(group.Select(listing => listing.EstimatedMonthlyRevenue)),
                AverageTrips = AverageDouble(group.Select(listing => listing.Trips.HasValue ? (double?)listing.Trips.Value : null)),
                AverageFavorites = AverageDouble(group.Select(listing => listing.Favorites.HasValue ? (double?)listing.Favorites.Value : null)),
                AverageReviews = AverageDouble(group.Select(listing => listing.Reviews.HasValue ? (double?)listing.Reviews.Value : null)),
            })
            .Where(group => group.ListingCount >= minimumGroupSize)
            .ToList();

        if (groupedStats.Count == 0 && minimumGroupSize > 1)
        {
            return BuildInsights(listings, groupingSelector, fallbackName, 1);
        }

        var revenueRange = GetRange(groupedStats.Select(group => (double)(group.AverageMonthlyRevenue ?? 0m)));
        var utilizationRange = GetRange(groupedStats.Select(group => group.AverageUtilization ?? 0d));
        var tripsRange = GetRange(groupedStats.Select(group => group.AverageTrips ?? 0d));
        var favoritesRange = GetRange(groupedStats.Select(group => group.AverageFavorites ?? 0d));
        var reviewsRange = GetRange(groupedStats.Select(group => group.AverageReviews ?? 0d));
        var competitionRange = GetRange(groupedStats.Select(group => (double)group.ListingCount));

        var overallRevenue = AverageDecimal(groupedStats.Select(group => group.AverageMonthlyRevenue));
        var overallUtilization = AverageDouble(groupedStats.Select(group => group.AverageUtilization));
        var overallTrips = AverageDouble(groupedStats.Select(group => group.AverageTrips));
        var overallFavorites = AverageDouble(groupedStats.Select(group => group.AverageFavorites));

        return groupedStats
            .Select(group =>
            {
                var revenueScore = Normalize((double)(group.AverageMonthlyRevenue ?? 0m), revenueRange);
                var utilizationScore = Normalize(group.AverageUtilization ?? 0d, utilizationRange);
                var tripsScore = Normalize(group.AverageTrips ?? 0d, tripsRange);
                var favoritesScore = Normalize(group.AverageFavorites ?? 0d, favoritesRange);
                var reviewsScore = Normalize(group.AverageReviews ?? 0d, reviewsRange);
                var competitionScore = 1d - Normalize(group.ListingCount, competitionRange);

                var score = (revenueScore * 0.34d)
                    + (utilizationScore * 0.24d)
                    + (tripsScore * 0.14d)
                    + (favoritesScore * 0.10d)
                    + (reviewsScore * 0.10d)
                    + (competitionScore * 0.08d);

                return new RankedInsight
                {
                    GroupName = group.GroupName,
                    ListingCount = group.ListingCount,
                    AverageDailyPrice = group.AverageDailyPrice,
                    AverageUtilization = group.AverageUtilization,
                    AverageMonthlyRevenue = group.AverageMonthlyRevenue,
                    AverageTrips = group.AverageTrips,
                    AverageFavorites = group.AverageFavorites,
                    AverageReviews = group.AverageReviews,
                    Score = Math.Round(score * 100d, 1),
                    Takeaway = BuildTakeaway(group, overallRevenue, overallUtilization, overallTrips, overallFavorites),
                };
            })
            .OrderByDescending(group => group.Score)
            .ThenByDescending(group => group.AverageMonthlyRevenue ?? 0m)
            .ThenByDescending(group => group.AverageUtilization ?? 0d)
            .ToList();
    }

    private static IReadOnlyList<string> BuildNarrativeInsights(
        SummaryMetrics summary,
        IReadOnlyList<RankedInsight> topVehicleTypes,
        IReadOnlyList<RankedInsight> topModels,
        IReadOnlyList<RankedInsight> topMarkets,
        IReadOnlyList<RankedInsight> topHosts,
        IReadOnlyList<SeasonalityInsight> seasonalityInsights,
        RecommendationSummary recommendation)
    {
        var insights = new List<string>();
        var currencySymbol = summary.CurrencySymbol;

        var topVehicleType = topVehicleTypes.FirstOrDefault();
        if (topVehicleType is not null)
        {
            insights.Add(
                $"{topVehicleType.GroupName} is the strongest vehicle segment in the current view, averaging {FormatCurrency(topVehicleType.AverageMonthlyRevenue, currencySymbol)} in monthly revenue with {FormatPercent(topVehicleType.AverageUtilization)} utilization.");
        }

        var topModel = topModels.FirstOrDefault();
        if (topModel is not null)
        {
            insights.Add(
                $"{topModel.GroupName} stands out at the model level. Its score is {topModel.Score:N1}/100 across {topModel.ListingCount:N0} comparable listings, and the signal reads: {topModel.Takeaway.ToLowerInvariant()}.");
        }

        if (!string.IsNullOrWhiteSpace(recommendation.RecommendedCandidate)
            && !recommendation.RecommendedCandidate.Equals("Need data", StringComparison.OrdinalIgnoreCase))
        {
            insights.Add(
                $"{recommendation.RecommendedCandidate} is the best visible car to inspect first. {recommendation.RecommendationReason}");
        }

        var topMarket = topMarkets
            .OrderByDescending(market => market.AverageUtilization ?? 0d)
            .ThenByDescending(market => market.AverageMonthlyRevenue ?? 0m)
            .FirstOrDefault();

        if (topMarket is not null)
        {
            insights.Add(
                $"{topMarket.GroupName} currently shows the hottest demand, with {FormatPercent(topMarket.AverageUtilization)} average utilization and {FormatCurrency(topMarket.AverageMonthlyRevenue, currencySymbol)} estimated monthly revenue.");
        }

        var topHost = topHosts.FirstOrDefault();
        if (topHost is not null)
        {
            insights.Add(
                $"{topHost.GroupName} is setting the host benchmark in this view, with {FormatCurrency(topHost.AverageMonthlyRevenue, currencySymbol)} average monthly revenue and {topHost.ListingCount:N0} visible listings.");
        }

        var strongestTrend = seasonalityInsights
            .OrderByDescending(insight => insight.TrendPercent)
            .FirstOrDefault();

        if (strongestTrend is not null)
        {
            insights.Add(
                $"{strongestTrend.Scope}: {strongestTrend.GroupName} is showing the strongest revenue momentum right now, with a {strongestTrend.TrendPercent:+0;-0;0}% trend and a seasonality read of {strongestTrend.SeasonalityScore:N0}.");
        }

        if (summary.AverageDailyPrice.HasValue && summary.AverageUtilization.HasValue)
        {
            insights.Add(
                $"Across the visible data, the market is averaging {FormatCurrency(summary.AverageDailyPrice, currencySymbol)} per day at {FormatPercent(summary.AverageUtilization)} utilization. That baseline helps you judge whether a candidate vehicle is underpriced, overpriced, or right on market.");
        }

        return insights;
    }

    private static IReadOnlyList<RankedInsight> BuildHostInsights(IReadOnlyList<TuroListing> listings)
    {
        var groupedHosts = listings
            .Where(listing => !string.IsNullOrWhiteSpace(listing.HostName))
            .GroupBy(listing => listing.HostLabel)
            .Select(group => new GroupStats
            {
                GroupName = group.Key,
                ListingCount = group.Count(),
                AverageDailyPrice = AverageDecimal(group.Select(listing => listing.DailyPrice)),
                AverageUtilization = AverageDouble(group.Select(listing => listing.UtilizationRate)),
                AverageMonthlyRevenue = AverageDecimal(group.Select(listing => listing.EstimatedMonthlyRevenue)),
                AverageTrips = AverageDouble(group.Select(listing => listing.Trips.HasValue ? (double?)listing.Trips.Value : null)),
                AverageFavorites = AverageDouble(group.Select(listing => listing.Favorites.HasValue ? (double?)listing.Favorites.Value : null)),
                AverageReviews = AverageDouble(group.Select(listing => listing.Reviews.HasValue ? (double?)listing.Reviews.Value : null)),
            })
            .ToList();

        var revenueRange = GetRange(groupedHosts.Select(group => (double)(group.AverageMonthlyRevenue ?? 0m)));
        var utilizationRange = GetRange(groupedHosts.Select(group => group.AverageUtilization ?? 0d));
        var favoritesRange = GetRange(groupedHosts.Select(group => group.AverageFavorites ?? 0d));
        var reviewsRange = GetRange(groupedHosts.Select(group => group.AverageReviews ?? 0d));
        var scaleRange = GetRange(groupedHosts.Select(group => (double)group.ListingCount));

        return groupedHosts
            .Select(group =>
            {
                var score = (Normalize((double)(group.AverageMonthlyRevenue ?? 0m), revenueRange) * 0.36d)
                    + (Normalize(group.AverageUtilization ?? 0d, utilizationRange) * 0.24d)
                    + (Normalize(group.AverageFavorites ?? 0d, favoritesRange) * 0.12d)
                    + (Normalize(group.AverageReviews ?? 0d, reviewsRange) * 0.16d)
                    + (Normalize(group.ListingCount, scaleRange) * 0.12d);

                return new RankedInsight
                {
                    GroupName = group.GroupName,
                    ListingCount = group.ListingCount,
                    AverageDailyPrice = group.AverageDailyPrice,
                    AverageUtilization = group.AverageUtilization,
                    AverageMonthlyRevenue = group.AverageMonthlyRevenue,
                    AverageTrips = group.AverageTrips,
                    AverageFavorites = group.AverageFavorites,
                    AverageReviews = group.AverageReviews,
                    Score = Math.Round(score * 100d, 1),
                    Takeaway = BuildHostTakeaway(group),
                };
            })
            .OrderByDescending(group => group.Score)
            .ThenByDescending(group => group.AverageMonthlyRevenue ?? 0m)
            .ThenByDescending(group => group.AverageReviews ?? 0d)
            .ToList();
    }

    private static IReadOnlyList<SeasonalityInsight> BuildSeasonalityInsights(IReadOnlyList<TuroListing> listings)
    {
        var builders = new (string Scope, Func<TuroListing, string> Selector, int MinimumGroupSize)[]
        {
            ("Model", listing => listing.ModelKey, listings.Count >= 12 ? 2 : 1),
            ("Vehicle type", listing => listing.VehicleType, 1),
            ("Market", listing => listing.Market, 1),
        };

        return builders
            .SelectMany(builder => BuildSeasonalityInsightsForScope(listings, builder.Scope, builder.Selector, builder.MinimumGroupSize))
            .OrderByDescending(insight => Math.Abs(insight.TrendPercent))
            .ThenByDescending(insight => insight.SeasonalityScore)
            .ThenByDescending(insight => insight.AverageMonthlyRevenue ?? 0m)
            .ToList();
    }

    private static IReadOnlyList<SeasonalityInsight> BuildSeasonalityInsightsForScope(
        IReadOnlyList<TuroListing> listings,
        string scope,
        Func<TuroListing, string> selector,
        int minimumGroupSize)
    {
        return listings
            .Where(listing => listing.MonthlyRevenueHistory.Count > 0)
            .GroupBy(listing =>
            {
                var key = selector(listing)?.Trim();
                return string.IsNullOrWhiteSpace(key) ? $"Unknown {scope.ToLowerInvariant()}" : key;
            })
            .Select(group => new
            {
                GroupName = group.Key,
                Listings = group.ToList(),
                MonthlySeries = BuildMonthlySeries(group),
            })
            .Where(group => group.Listings.Count >= minimumGroupSize && group.MonthlySeries.Count >= 3)
            .Select(group =>
            {
                var monthlyValues = group.MonthlySeries.Select(point => point.Revenue).ToList();
                var average = monthlyValues.Average();
                var min = monthlyValues.Min();
                var max = monthlyValues.Max();
                var seasonalityScore = average <= 0m
                    ? 0d
                    : Math.Round((double)((max - min) / average) * 100d, 1);
                var trendPercent = CalculateTrendPercent(monthlyValues);

                return new SeasonalityInsight
                {
                    Scope = scope,
                    GroupName = group.GroupName,
                    ListingCount = group.Listings.Count,
                    AverageMonthlyRevenue = decimal.Round(average, 0, MidpointRounding.AwayFromZero),
                    AverageUtilization = AverageDouble(group.Listings.Select(listing => listing.UtilizationRate)),
                    SeasonalityScore = seasonalityScore,
                    TrendPercent = trendPercent,
                    TrendLabel = BuildTrendLabel(trendPercent),
                    RevenueSparkline = BuildSparkline(monthlyValues),
                    Takeaway = BuildSeasonalityTakeaway(trendPercent, seasonalityScore),
                };
            })
            .ToList();
    }

    private static IReadOnlyList<MarketGapOpportunity> BuildMarketGaps(IReadOnlyList<TuroListing> listings)
    {
        var groupedStats = listings
            .GroupBy(listing => new
            {
                Market = string.IsNullOrWhiteSpace(listing.Market) ? "Unknown market" : listing.Market,
                VehicleType = string.IsNullOrWhiteSpace(listing.VehicleType) ? "Unclassified" : listing.VehicleType,
            })
            .Select(group => new GapGroupStats
            {
                Market = group.Key.Market,
                VehicleType = group.Key.VehicleType,
                ListingCount = group.Count(),
                AverageDailyPrice = AverageDecimal(group.Select(listing => listing.DailyPrice)),
                AverageUtilization = AverageDouble(group.Select(listing => listing.UtilizationRate)),
                AverageMonthlyRevenue = AverageDecimal(group.Select(listing => listing.EstimatedMonthlyRevenue)),
            })
            .ToList();

        var revenueRange = GetRange(groupedStats.Select(group => (double)(group.AverageMonthlyRevenue ?? 0m)));
        var utilizationRange = GetRange(groupedStats.Select(group => group.AverageUtilization ?? 0d));
        var competitionRange = GetRange(groupedStats.Select(group => (double)group.ListingCount));

        var overallRevenue = AverageDecimal(groupedStats.Select(group => group.AverageMonthlyRevenue));
        var overallUtilization = AverageDouble(groupedStats.Select(group => group.AverageUtilization));

        return groupedStats
            .Select(group =>
            {
                var revenueScore = Normalize((double)(group.AverageMonthlyRevenue ?? 0m), revenueRange);
                var utilizationScore = Normalize(group.AverageUtilization ?? 0d, utilizationRange);
                var competitionScore = 1d - Normalize(group.ListingCount, competitionRange);

                var gapScore = (utilizationScore * 0.45d)
                    + (revenueScore * 0.35d)
                    + (competitionScore * 0.20d);

                return new MarketGapOpportunity
                {
                    Market = group.Market,
                    VehicleType = group.VehicleType,
                    ListingCount = group.ListingCount,
                    AverageDailyPrice = group.AverageDailyPrice,
                    AverageUtilization = group.AverageUtilization,
                    AverageMonthlyRevenue = group.AverageMonthlyRevenue,
                    GapScore = Math.Round(gapScore * 100d, 1),
                    OpportunitySummary = BuildGapSummary(group, overallRevenue, overallUtilization),
                };
            })
            .OrderByDescending(group => group.GapScore)
            .ThenByDescending(group => group.AverageUtilization ?? 0d)
            .ThenByDescending(group => group.AverageMonthlyRevenue ?? 0m)
            .ToList();
    }

    private static IReadOnlyList<TuroListing> BuildVisibleListings(
        IReadOnlyList<TuroListing> listings,
        IReadOnlyList<ListingRecommendation> recommendedListings)
    {
        if (recommendedListings.Count == 0)
        {
            return listings.Take(250).ToList();
        }

        var featuredListings = recommendedListings
            .Take(20)
            .Select(recommendation => recommendation.Listing)
            .ToList();

        var featuredSet = featuredListings.ToHashSet();

        return featuredListings
            .Concat(listings.Where(listing => !featuredSet.Contains(listing)))
            .Take(250)
            .ToList();
    }

    private static IReadOnlyList<ListingRecommendation> BuildListingRecommendations(
        IReadOnlyList<TuroListing> listings,
        IReadOnlyList<RankedInsight> topVehicleTypes,
        IReadOnlyList<RankedInsight> topModels,
        IReadOnlyList<RankedInsight> topMarkets,
        IReadOnlyList<MarketGapOpportunity> marketGaps)
    {
        if (listings.Count == 0)
        {
            return Array.Empty<ListingRecommendation>();
        }

        var vehicleTypeScores = BuildScoreLookup(topVehicleTypes, insight => insight.GroupName, insight => insight.Score);
        var modelScores = BuildScoreLookup(topModels, insight => insight.GroupName, insight => insight.Score);
        var marketScores = BuildScoreLookup(topMarkets, insight => insight.GroupName, insight => insight.Score);
        var marketGapScores = BuildScoreLookup(marketGaps, insight => BuildGapKey(insight.Market, insight.VehicleType), insight => insight.GapScore);

        var revenueRange = GetRange(listings.Select(listing => (double)(listing.EstimatedMonthlyRevenue ?? listing.Last12MonthRevenue ?? 0m)));
        var utilizationRange = GetRange(listings.Select(listing => listing.UtilizationRate ?? 0d));
        var tripsRange = GetRange(listings.Select(listing => listing.Trips.HasValue ? listing.Trips.Value : 0d));
        var favoritesRange = GetRange(listings.Select(listing => listing.Favorites.HasValue ? listing.Favorites.Value : 0d));
        var reviewsRange = GetRange(listings.Select(listing => listing.Reviews.HasValue ? listing.Reviews.Value : 0d));
        var ratingRange = GetRange(listings.Select(listing => listing.Rating ?? 0d));
        var roiRange = GetRange(listings.Select(listing => listing.ReturnOnInvestment ?? 0d));
        var efficiencyRange = GetRange(listings.Select(CalculateRevenueEfficiency));
        var convenienceRange = GetRange(listings.Select(CalculateConvenienceScore));
        var trendRange = GetRange(listings.Select(CalculateHistoryTrend));

        return listings
            .Select(listing =>
            {
                var modelScore = LookupScore(modelScores, listing.ModelKey);
                var vehicleTypeScore = LookupScore(vehicleTypeScores, listing.VehicleType);
                var marketScore = LookupScore(marketScores, listing.Market);
                var marketGapScore = LookupScore(marketGapScores, BuildGapKey(listing.Market, listing.VehicleType));

                var revenueScore = Normalize((double)(listing.EstimatedMonthlyRevenue ?? listing.Last12MonthRevenue ?? 0m), revenueRange);
                var utilizationScore = Normalize(listing.UtilizationRate ?? 0d, utilizationRange);
                var tripsScore = Normalize(listing.Trips.HasValue ? listing.Trips.Value : 0d, tripsRange);
                var favoritesScore = Normalize(listing.Favorites.HasValue ? listing.Favorites.Value : 0d, favoritesRange);
                var reviewsScore = Normalize(listing.Reviews.HasValue ? listing.Reviews.Value : 0d, reviewsRange);
                var ratingScore = Normalize(listing.Rating ?? 0d, ratingRange);
                var roiScore = Normalize(listing.ReturnOnInvestment ?? 0d, roiRange);
                var efficiencyScore = Normalize(CalculateRevenueEfficiency(listing), efficiencyRange);
                var convenienceScore = Normalize(CalculateConvenienceScore(listing), convenienceRange);
                var trendScore = Normalize(CalculateHistoryTrend(listing), trendRange);

                var recommendationScore = (revenueScore * 0.17d)
                    + (utilizationScore * 0.14d)
                    + ((modelScore / 100d) * 0.10d)
                    + ((vehicleTypeScore / 100d) * 0.08d)
                    + ((marketGapScore / 100d) * 0.08d)
                    + ((marketScore / 100d) * 0.04d)
                    + (tripsScore * 0.06d)
                    + (favoritesScore * 0.04d)
                    + (reviewsScore * 0.05d)
                    + (ratingScore * 0.05d)
                    + (roiScore * 0.05d)
                    + (efficiencyScore * 0.05d)
                    + (convenienceScore * 0.04d)
                    + (trendScore * 0.05d);

                return new ListingRecommendation
                {
                    Listing = listing,
                    RecommendationScore = Math.Round(recommendationScore * 100d, 1),
                    RecommendationBand = BuildRecommendationBand(recommendationScore * 100d),
                    RecommendationReason = BuildRecommendationReason(
                        listing,
                        modelScore / 100d,
                        vehicleTypeScore / 100d,
                        marketGapScore / 100d,
                        revenueScore,
                        utilizationScore,
                        efficiencyScore,
                        reviewsScore,
                        ratingScore,
                        trendScore),
                };
            })
            .OrderByDescending(recommendation => recommendation.RecommendationScore)
            .ThenByDescending(recommendation => recommendation.Listing.EstimatedMonthlyRevenue ?? recommendation.Listing.Last12MonthRevenue ?? 0m)
            .ThenByDescending(recommendation => recommendation.Listing.UtilizationRate ?? 0d)
            .ToList();
    }

    private static RecommendationSummary BuildRecommendationSummary(
        IReadOnlyList<RankedInsight> topVehicleTypes,
        IReadOnlyList<RankedInsight> topModels,
        IReadOnlyList<RankedInsight> topMarkets,
        IReadOnlyList<ListingRecommendation> recommendedListings)
    {
        var topVehicleType = topVehicleTypes.FirstOrDefault()?.GroupName ?? "Need data";
        var topModel = topModels.FirstOrDefault()?.GroupName ?? "Need data";
        var topMarket = topMarkets.FirstOrDefault()?.GroupName ?? "Need data";
        var topCandidate = recommendedListings.FirstOrDefault();

        if (topCandidate is null)
        {
            return new RecommendationSummary
            {
                RecommendedVehicleType = topVehicleType,
                RecommendedModel = topModel,
                RecommendedMarket = topMarket,
            };
        }

        var segmentLabel = topModel.Equals("Need data", StringComparison.OrdinalIgnoreCase)
            ? topVehicleType
            : $"{topModel} / {topVehicleType}";

        return new RecommendationSummary
        {
            RecommendedVehicleType = topVehicleType,
            RecommendedModel = topModel,
            RecommendedMarket = topCandidate.Market,
            RecommendedCandidate = topCandidate.VehicleName,
            RecommendedCandidateScoreText = string.Create(CultureInfo.InvariantCulture, $"{topCandidate.RecommendationScore:N1}/100"),
            Headline = $"Target {segmentLabel} in {topCandidate.Market}. {topCandidate.VehicleName} is the strongest visible car right now.",
            RecommendationReason = topCandidate.RecommendationReason,
        };
    }

    private static string BuildRecommendationBand(double recommendationScore)
    {
        return recommendationScore switch
        {
            >= 78d => "Prime target",
            >= 66d => "Strong follow-up",
            >= 54d => "Watch list",
            _ => "Monitor",
        };
    }

    private static string BuildRecommendationReason(
        TuroListing listing,
        double modelScore,
        double vehicleTypeScore,
        double marketGapScore,
        double revenueScore,
        double utilizationScore,
        double efficiencyScore,
        double reviewsScore,
        double ratingScore,
        double trendScore)
    {
        var reasons = new List<string>();

        if (modelScore >= 0.75d)
        {
            reasons.Add($"it fits a top-performing {listing.ModelKey} pattern");
        }
        else if (vehicleTypeScore >= 0.75d)
        {
            reasons.Add($"the {listing.VehicleType.ToLowerInvariant()} segment is winning this filter");
        }

        if (marketGapScore >= 0.70d)
        {
            reasons.Add($"its {listing.Market} niche still looks under-supplied");
        }

        if (revenueScore >= 0.75d && utilizationScore >= 0.75d)
        {
            reasons.Add("it pairs strong revenue with strong utilization");
        }
        else if (revenueScore >= 0.75d)
        {
            reasons.Add("it is one of the higher-revenue listings in view");
        }
        else if (utilizationScore >= 0.75d)
        {
            reasons.Add("it is booking at a healthy utilization rate");
        }

        if (efficiencyScore >= 0.75d)
        {
            reasons.Add("it converts price into monthly revenue efficiently");
        }

        if (reviewsScore >= 0.75d || ratingScore >= 0.75d)
        {
            reasons.Add("it has proof from review depth and guest ratings");
        }

        if (trendScore >= 0.75d)
        {
            reasons.Add("recent revenue history is moving in the right direction");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("it is well balanced across segment strength and listing-level demand");
        }

        var firstReason = char.ToUpperInvariant(reasons[0][0]) + reasons[0][1..];
        return reasons.Count > 1
            ? $"{firstReason}, and {reasons[1]}."
            : $"{firstReason}.";
    }

    private static string BuildGapKey(string market, string vehicleType)
    {
        return $"{market}||{vehicleType}";
    }

    private static double LookupScore(IReadOnlyDictionary<string, double> scores, string key)
    {
        return scores.TryGetValue(key, out var score) ? score : 0d;
    }

    private static Dictionary<string, double> BuildScoreLookup<T>(
        IEnumerable<T> insights,
        Func<T, string> keySelector,
        Func<T, double> scoreSelector)
    {
        return insights
            .Select(insight => new
            {
                Key = keySelector(insight).Trim(),
                Score = scoreSelector(insight),
            })
            .Where(insight => !string.IsNullOrWhiteSpace(insight.Key))
            .GroupBy(insight => insight.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Max(insight => insight.Score),
                StringComparer.OrdinalIgnoreCase);
    }

    private static double CalculateRevenueEfficiency(TuroListing listing)
    {
        var revenue = listing.EstimatedMonthlyRevenue ?? listing.Last12MonthRevenue;
        return listing.DailyPrice is > 0m && revenue is > 0m
            ? (double)(revenue.Value / listing.DailyPrice.Value)
            : 0d;
    }

    private static double CalculateConvenienceScore(TuroListing listing)
    {
        var airportScore = string.IsNullOrWhiteSpace(listing.AirportDelivery) ? 0d : 30d;
        var instantBookScore = string.IsNullOrWhiteSpace(listing.InstantBookLocations) ? 0d : 25d;
        var extrasScore = Math.Min(CountCsvItems(listing.Extras) * 4d, 20d);
        var badgesScore = Math.Min(CountCsvItems(listing.Badges) * 3d, 25d);
        return Math.Round(airportScore + instantBookScore + extrasScore + badgesScore, 0);
    }

    private static double CalculateHistoryTrend(TuroListing listing)
    {
        return listing.MonthlyRevenueHistory.Count >= 3
            ? CalculateTrendPercent(listing.MonthlyRevenueHistory.Select(point => point.Revenue).ToList())
            : 0d;
    }

    private static string BuildTakeaway(
        GroupStats group,
        decimal? overallRevenue,
        double? overallUtilization,
        double? overallTrips,
        double? overallFavorites)
    {
        var beatsRevenue = group.AverageMonthlyRevenue.HasValue
            && overallRevenue.HasValue
            && group.AverageMonthlyRevenue.Value >= overallRevenue.Value * 1.10m;

        var beatsUtilization = group.AverageUtilization.HasValue
            && overallUtilization.HasValue
            && group.AverageUtilization.Value >= overallUtilization.Value * 1.08d;

        var beatsTrips = group.AverageTrips.HasValue
            && overallTrips.HasValue
            && group.AverageTrips.Value >= overallTrips.Value * 1.10d;

        var beatsFavorites = group.AverageFavorites.HasValue
            && overallFavorites.HasValue
            && group.AverageFavorites.Value >= overallFavorites.Value * 1.12d;

        if (beatsRevenue && beatsUtilization)
        {
            return "High revenue and strong occupancy";
        }

        if (beatsFavorites && beatsUtilization)
        {
            return "Strong demand signals with healthy booking velocity";
        }

        if (beatsUtilization && !beatsRevenue)
        {
            return "Excellent occupancy with room to test price increases";
        }

        if (beatsRevenue && !beatsUtilization)
        {
            return "Strong revenue, but occupancy is worth watching";
        }

        if (beatsTrips && group.ListingCount <= 3)
        {
            return "Small but promising niche with strong trip velocity";
        }

        if (group.ListingCount >= 6)
        {
            return "Competitive segment with dependable demand";
        }

        return "Balanced segment with average market performance";
    }

    private static string BuildHostTakeaway(GroupStats group)
    {
        if (group.GroupName.Contains("All-Star", StringComparison.OrdinalIgnoreCase)
            && group.AverageMonthlyRevenue.HasValue)
        {
            return "All-Star host with visible pricing power and repeatable performance";
        }

        if (group.ListingCount >= 4 && group.AverageReviews is > 50d)
        {
            return "Scaled host with meaningful review depth";
        }

        if (group.AverageFavorites is > 80d)
        {
            return "Strong browse-to-book demand signal across listings";
        }

        return "Worth watching as a host benchmark";
    }

    private static string BuildGapSummary(
        GapGroupStats group,
        decimal? overallRevenue,
        double? overallUtilization)
    {
        var highRevenue = group.AverageMonthlyRevenue.HasValue
            && overallRevenue.HasValue
            && group.AverageMonthlyRevenue.Value >= overallRevenue.Value * 1.10m;

        var highUtilization = group.AverageUtilization.HasValue
            && overallUtilization.HasValue
            && group.AverageUtilization.Value >= overallUtilization.Value * 1.08d;

        if (highRevenue && highUtilization && group.ListingCount <= 3)
        {
            return "High-demand niche with strong revenue and relatively light competition";
        }

        if (highUtilization && group.ListingCount <= 3)
        {
            return "Demand looks stronger than the visible supply in this pocket";
        }

        if (highRevenue && group.ListingCount <= 4)
        {
            return "Premium pricing pocket that is not yet overcrowded";
        }

        if (highUtilization && group.ListingCount >= 5)
        {
            return "Demand is strong, but competition is already building";
        }

        return "Worth exploring as a market gap candidate";
    }

    private static IReadOnlyList<MonthlyRevenuePoint> BuildMonthlySeries(IEnumerable<TuroListing> listings)
    {
        return listings
            .SelectMany(listing => listing.MonthlyRevenueHistory)
            .GroupBy(point => point.Period)
            .Select(group => new MonthlyRevenuePoint
            {
                Period = group.Key,
                Label = group.Key.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                Revenue = decimal.Round(group.Average(point => point.Revenue), 0, MidpointRounding.AwayFromZero),
            })
            .OrderBy(point => point.Period)
            .ToList();
    }

    private static double CalculateTrendPercent(IReadOnlyList<decimal> monthlyValues)
    {
        if (monthlyValues.Count < 2)
        {
            return 0d;
        }

        var windowSize = Math.Min(3, monthlyValues.Count / 2);
        if (windowSize == 0)
        {
            windowSize = 1;
        }

        var earlyAverage = monthlyValues.Take(windowSize).Average();
        var recentAverage = monthlyValues.TakeLast(windowSize).Average();

        if (earlyAverage == 0m)
        {
            return recentAverage > 0m ? 100d : 0d;
        }

        return Math.Round((double)((recentAverage - earlyAverage) / earlyAverage) * 100d, 1);
    }

    private static string BuildTrendLabel(double trendPercent)
    {
        return trendPercent switch
        {
            >= 20d => "Surging",
            >= 8d => "Improving",
            <= -20d => "Cooling hard",
            <= -8d => "Softening",
            _ => "Stable",
        };
    }

    private static string BuildSeasonalityTakeaway(double trendPercent, double seasonalityScore)
    {
        if (trendPercent >= 15d && seasonalityScore <= 35d)
        {
            return "Momentum is improving without extreme month-to-month swings";
        }

        if (trendPercent <= -15d)
        {
            return "Recent revenue is cooling, so buy assumptions should lean conservative";
        }

        if (seasonalityScore >= 65d)
        {
            return "Highly seasonal pocket that needs strong peak-month execution";
        }

        return "Relatively steady revenue pattern in the visible history";
    }

    private static int CountCsvItems(string rawText)
    {
        return string.IsNullOrWhiteSpace(rawText)
            ? 0
            : rawText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    private static (double Min, double Max) GetRange(IEnumerable<double> values)
    {
        var snapshot = values.ToList();
        return snapshot.Count == 0
            ? (0d, 0d)
            : (snapshot.Min(), snapshot.Max());
    }

    private static double Normalize(double value, (double Min, double Max) range)
    {
        if (range.Max <= range.Min)
        {
            return value > 0d ? 0.5d : 0d;
        }

        return Math.Clamp((value - range.Min) / (range.Max - range.Min), 0d, 1d);
    }

    private static decimal? AverageDecimal(IEnumerable<decimal?> values)
    {
        var usable = values
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        return usable.Count == 0
            ? null
            : decimal.Round(usable.Average(), 0, MidpointRounding.AwayFromZero);
    }

    private static double? AverageDouble(IEnumerable<double?> values)
    {
        var usable = values
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        return usable.Count == 0
            ? null
            : Math.Round(usable.Average(), 2);
    }

    private static string DetermineCurrencySymbol(IEnumerable<TuroListing> listings)
    {
        return listings
            .Select(listing => listing.CurrencySymbol)
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .GroupBy(symbol => symbol)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault() ?? "$";
    }

    private static string BuildSparkline(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            return string.Empty;
        }

        const string levels = "▁▂▃▄▅▆▇█";
        var min = values.Min();
        var max = values.Max();

        if (max <= min)
        {
            return new string(levels[3], values.Count);
        }

        return string.Concat(values.Select(value =>
        {
            var normalized = (double)((value - min) / (max - min));
            var levelIndex = (int)Math.Round(normalized * (levels.Length - 1), MidpointRounding.AwayFromZero);
            levelIndex = Math.Clamp(levelIndex, 0, levels.Length - 1);
            return levels[levelIndex];
        }));
    }

    private static string FormatCurrency(decimal? value, string currencySymbol)
    {
        return value.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{currencySymbol}{value.Value:N0}")
            : "n/a";
    }

    private static string FormatPercent(double? value)
    {
        return value.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{value.Value:P0}")
            : "n/a";
    }

    private sealed class GroupStats
    {
        public required string GroupName { get; init; }

        public required int ListingCount { get; init; }

        public decimal? AverageDailyPrice { get; init; }

        public double? AverageUtilization { get; init; }

        public decimal? AverageMonthlyRevenue { get; init; }

        public double? AverageTrips { get; init; }

        public double? AverageFavorites { get; init; }

        public double? AverageReviews { get; init; }
    }

    private sealed class GapGroupStats
    {
        public required string Market { get; init; }

        public required string VehicleType { get; init; }

        public required int ListingCount { get; init; }

        public decimal? AverageDailyPrice { get; init; }

        public double? AverageUtilization { get; init; }

        public decimal? AverageMonthlyRevenue { get; init; }
    }
}

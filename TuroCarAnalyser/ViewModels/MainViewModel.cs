using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media.Imaging;
using TuroCarAnalyser.Infrastructure;
using TuroCarAnalyser.Models;
using TuroCarAnalyser.Services;

namespace TuroCarAnalyser.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const string AllMarketsLabel = "All markets";
    private const string AllSourcesLabel = "All sources";

    private readonly CsvImportService _csvImportService = new();
    private readonly AnalysisEngine _analysisEngine = new();
    private readonly InvestmentAnalysisService _investmentAnalysisService = new();
    private readonly KnowledgeBaseService _knowledgeBaseService = new();

    private readonly List<string> _warningMessages = [];
    private List<TuroListing> _allListings = [];
    private List<TuroListing> _filteredListings = [];
    private List<TuroListing> _knowledgeBaseListings = [];
    private bool _suppressFilterRefresh;
    private bool _suppressSelectionAnalysisRefresh;
    private string _activeCurrencySymbol = "$";

    private string _selectedMarket = AllMarketsLabel;
    private string _selectedSource = AllSourcesLabel;
    private TuroListing? _selectedListing;

    private string _statusMessage = "Import CSV files to start profiling the market.";
    private string _importSummary = "No CSVs loaded yet. Use the import button or drop files into the window.";
    private string _importNotesText = "The importer accepts CSV exports from tools like Raptor Explorer and RentScout, then maps their columns into one common analysis model.";
    private string _listingCountText = "0";
    private string _marketCountText = "0";
    private string _sourceCountText = "0";
    private string _averageDailyPriceText = "n/a";
    private string _averageUtilizationText = "n/a";
    private string _averageMonthlyRevenueText = "n/a";
    private string _bestVehicleTypeText = "Need data";
    private string _recommendedModelText = "Need data";
    private string _recommendedCandidateText = "Need data";
    private string _recommendedCandidateScoreText = "n/a";
    private string _recommendedCandidateReasonText = "Recommendations will appear here once the app can compare segment strength and listing-level demand.";
    private string _recommendationHeadlineText = "Import data to generate a car recommendation.";
    private string _knowledgeBaseStatusText = "Knowledge base is empty. Imported listings will be stored locally for future baselines.";
    private string _historicalComparisonText = "Recommendations are using only the current session until the app has stored historical listings.";

    private string _purchasePriceInput = "35000";
    private string _downPaymentInput = "7000";
    private string _loanAprInput = "7.9";
    private string _loanTermMonthsInput = "60";
    private string _insurancePerMonthInput = "180";
    private string _maintenancePerMonthInput = "95";
    private string _cleaningPerMonthInput = "80";
    private string _otherFixedCostsInput = "40";
    private string _turoFeePercentInput = "25";

    private string _selectedListingTitleText = "Choose a listing";
    private string _selectedListingContextText = "Select a row in the Listings tab to inspect ROI and nearby comparables.";
    private string _selectedListingRevenueText = "n/a";
    private string _selectedListingDailyPriceText = "n/a";
    private string _selectedListingUtilizationText = "n/a";
    private string _selectedListingHostText = "Host details will appear here once a listing is selected.";
    private string _selectedListingListingMetaText = "Pick a listing to see maturity, transmission, and market setup.";
    private string _selectedListingTrendText = "Trend: n/a";
    private string _selectedListingDemandText = "Demand score: n/a";
    private string _selectedListingConvenienceText = "Convenience score: n/a";
    private string _selectedListingDiscountText = "Discount strategy: n/a";
    private string _selectedListingHostQualityText = "Host quality: n/a";
    private string _selectedListingHistorySummaryText = "Monthly revenue history will appear here when the source file includes it.";
    private string _selectedListingSparklineText = string.Empty;
    private string _selectedListingFeatureText = "Feature and delivery setup details appear here once a candidate is selected.";
    private string _selectedListingExternalUrl = string.Empty;
    private string _selectedListingImageUrl = string.Empty;
    private BitmapImage? _selectedListingImageSource;
    private string _roiInitialCashText = "n/a";
    private string _roiLoanPaymentText = "n/a";
    private string _roiTuroFeesText = "n/a";
    private string _roiOperatingCostsText = "n/a";
    private string _roiNetCashFlowText = "n/a";
    private string _roiBreakEvenRevenueText = "n/a";
    private string _roiBreakEvenUtilizationText = "n/a";
    private string _roiAnnualYieldText = "n/a";
    private string _roiCashOnCashText = "n/a";
    private string _roiSummaryText = "Add acquisition assumptions and choose a listing to see net cash flow, yield, and break-even estimates.";
    private string _comparableSummaryText = "Comparables will appear here once a listing is selected.";
    private ListingRecommendation? _selectedRecommendation;

    public MainViewModel()
    {
        AvailableMarkets.Add(AllMarketsLabel);
        AvailableSources.Add(AllSourcesLabel);

        NarrativeInsights.Add("Import one or more CSV files to generate vehicle-type, host, and model-level performance rankings.");
        NarrativeInsights.Add("Richer exports also unlock trend, seasonality, convenience, and selected-vehicle detail analysis for a chosen candidate.");

        ReloadKnowledgeBase();
    }

    public ObservableCollection<string> AvailableMarkets { get; } = [];

    public ObservableCollection<string> AvailableSources { get; } = [];

    public ObservableCollection<RankedInsight> TopModels { get; } = [];

    public ObservableCollection<RankedInsight> TopVehicleTypes { get; } = [];

    public ObservableCollection<RankedInsight> TopMarkets { get; } = [];

    public ObservableCollection<MarketGapOpportunity> MarketGaps { get; } = [];

    public ObservableCollection<RankedInsight> TopHosts { get; } = [];

    public ObservableCollection<SeasonalityInsight> SeasonalityInsights { get; } = [];

    public ObservableCollection<ComparableVehicle> ComparableVehicles { get; } = [];

    public ObservableCollection<MonthlyRevenuePoint> SelectedListingRevenueHistory { get; } = [];

    public ObservableCollection<TuroListing> VisibleListings { get; } = [];

    public ObservableCollection<ListingRecommendation> RecommendedListings { get; } = [];

    public ObservableCollection<string> NarrativeInsights { get; } = [];

    public string SelectedMarket
    {
        get => _selectedMarket;
        set
        {
            if (SetProperty(ref _selectedMarket, value) && !_suppressFilterRefresh)
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value) && !_suppressFilterRefresh)
            {
                ApplyFilters();
            }
        }
    }

    public TuroListing? SelectedListing
    {
        get => _selectedListing;
        set
        {
            if (SetProperty(ref _selectedListing, value) && !_suppressSelectionAnalysisRefresh)
            {
                RefreshSelectionAnalysis();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ImportSummary
    {
        get => _importSummary;
        private set => SetProperty(ref _importSummary, value);
    }

    public string ImportNotesText
    {
        get => _importNotesText;
        private set => SetProperty(ref _importNotesText, value);
    }

    public string ListingCountText
    {
        get => _listingCountText;
        private set => SetProperty(ref _listingCountText, value);
    }

    public string MarketCountText
    {
        get => _marketCountText;
        private set => SetProperty(ref _marketCountText, value);
    }

    public string SourceCountText
    {
        get => _sourceCountText;
        private set => SetProperty(ref _sourceCountText, value);
    }

    public string AverageDailyPriceText
    {
        get => _averageDailyPriceText;
        private set => SetProperty(ref _averageDailyPriceText, value);
    }

    public string AverageUtilizationText
    {
        get => _averageUtilizationText;
        private set => SetProperty(ref _averageUtilizationText, value);
    }

    public string AverageMonthlyRevenueText
    {
        get => _averageMonthlyRevenueText;
        private set => SetProperty(ref _averageMonthlyRevenueText, value);
    }

    public string BestVehicleTypeText
    {
        get => _bestVehicleTypeText;
        private set => SetProperty(ref _bestVehicleTypeText, value);
    }

    public string RecommendedModelText
    {
        get => _recommendedModelText;
        private set => SetProperty(ref _recommendedModelText, value);
    }

    public string RecommendedCandidateText
    {
        get => _recommendedCandidateText;
        private set => SetProperty(ref _recommendedCandidateText, value);
    }

    public string RecommendedCandidateScoreText
    {
        get => _recommendedCandidateScoreText;
        private set => SetProperty(ref _recommendedCandidateScoreText, value);
    }

    public string RecommendedCandidateReasonText
    {
        get => _recommendedCandidateReasonText;
        private set => SetProperty(ref _recommendedCandidateReasonText, value);
    }

    public string RecommendationHeadlineText
    {
        get => _recommendationHeadlineText;
        private set => SetProperty(ref _recommendationHeadlineText, value);
    }

    public string KnowledgeBaseStatusText
    {
        get => _knowledgeBaseStatusText;
        private set => SetProperty(ref _knowledgeBaseStatusText, value);
    }

    public string HistoricalComparisonText
    {
        get => _historicalComparisonText;
        private set => SetProperty(ref _historicalComparisonText, value);
    }

    public ListingRecommendation? SelectedRecommendation
    {
        get => _selectedRecommendation;
        set
        {
            if (SetProperty(ref _selectedRecommendation, value)
                && value is not null
                && (SelectedListing is null || !IsSameListing(value.Listing, SelectedListing)))
            {
                SelectedListing = value.Listing;
            }
        }
    }

    public string PurchasePriceInput
    {
        get => _purchasePriceInput;
        set
        {
            if (SetProperty(ref _purchasePriceInput, value) && !_suppressSelectionAnalysisRefresh)
            {
                RefreshSelectionAnalysis();
            }
        }
    }

    public string DownPaymentInput
    {
        get => _downPaymentInput;
        set
        {
            if (SetProperty(ref _downPaymentInput, value) && !_suppressSelectionAnalysisRefresh)
            {
                RefreshSelectionAnalysis();
            }
        }
    }

    public string LoanAprInput
    {
        get => _loanAprInput;
        set
        {
            if (SetProperty(ref _loanAprInput, value) && !_suppressSelectionAnalysisRefresh)
            {
                RefreshSelectionAnalysis();
            }
        }
    }

    public string LoanTermMonthsInput
    {
        get => _loanTermMonthsInput;
        set
        {
            if (SetProperty(ref _loanTermMonthsInput, value) && !_suppressSelectionAnalysisRefresh)
            {
                RefreshSelectionAnalysis();
            }
        }
    }

    public string InsurancePerMonthInput
    {
        get => _insurancePerMonthInput;
        set
        {
            if (SetProperty(ref _insurancePerMonthInput, value) && !_suppressSelectionAnalysisRefresh)
            {
                RefreshSelectionAnalysis();
            }
        }
    }

    public string MaintenancePerMonthInput
    {
        get => _maintenancePerMonthInput;
        set
        {
            if (SetProperty(ref _maintenancePerMonthInput, value) && !_suppressSelectionAnalysisRefresh)
            {
                RefreshSelectionAnalysis();
            }
        }
    }

    public string CleaningPerMonthInput
    {
        get => _cleaningPerMonthInput;
        set
        {
            if (SetProperty(ref _cleaningPerMonthInput, value) && !_suppressSelectionAnalysisRefresh)
            {
                RefreshSelectionAnalysis();
            }
        }
    }

    public string OtherFixedCostsInput
    {
        get => _otherFixedCostsInput;
        set
        {
            if (SetProperty(ref _otherFixedCostsInput, value) && !_suppressSelectionAnalysisRefresh)
            {
                RefreshSelectionAnalysis();
            }
        }
    }

    public string TuroFeePercentInput
    {
        get => _turoFeePercentInput;
        set
        {
            if (SetProperty(ref _turoFeePercentInput, value) && !_suppressSelectionAnalysisRefresh)
            {
                RefreshSelectionAnalysis();
            }
        }
    }

    public string SelectedListingTitleText
    {
        get => _selectedListingTitleText;
        private set => SetProperty(ref _selectedListingTitleText, value);
    }

    public string SelectedListingContextText
    {
        get => _selectedListingContextText;
        private set => SetProperty(ref _selectedListingContextText, value);
    }

    public string SelectedListingRevenueText
    {
        get => _selectedListingRevenueText;
        private set => SetProperty(ref _selectedListingRevenueText, value);
    }

    public string SelectedListingDailyPriceText
    {
        get => _selectedListingDailyPriceText;
        private set => SetProperty(ref _selectedListingDailyPriceText, value);
    }

    public string SelectedListingUtilizationText
    {
        get => _selectedListingUtilizationText;
        private set => SetProperty(ref _selectedListingUtilizationText, value);
    }

    public string SelectedListingHostText
    {
        get => _selectedListingHostText;
        private set => SetProperty(ref _selectedListingHostText, value);
    }

    public string SelectedListingListingMetaText
    {
        get => _selectedListingListingMetaText;
        private set => SetProperty(ref _selectedListingListingMetaText, value);
    }

    public string SelectedListingTrendText
    {
        get => _selectedListingTrendText;
        private set => SetProperty(ref _selectedListingTrendText, value);
    }

    public string SelectedListingDemandText
    {
        get => _selectedListingDemandText;
        private set => SetProperty(ref _selectedListingDemandText, value);
    }

    public string SelectedListingConvenienceText
    {
        get => _selectedListingConvenienceText;
        private set => SetProperty(ref _selectedListingConvenienceText, value);
    }

    public string SelectedListingDiscountText
    {
        get => _selectedListingDiscountText;
        private set => SetProperty(ref _selectedListingDiscountText, value);
    }

    public string SelectedListingHostQualityText
    {
        get => _selectedListingHostQualityText;
        private set => SetProperty(ref _selectedListingHostQualityText, value);
    }

    public string SelectedListingHistorySummaryText
    {
        get => _selectedListingHistorySummaryText;
        private set => SetProperty(ref _selectedListingHistorySummaryText, value);
    }

    public string SelectedListingSparklineText
    {
        get => _selectedListingSparklineText;
        private set => SetProperty(ref _selectedListingSparklineText, value);
    }

    public string SelectedListingFeatureText
    {
        get => _selectedListingFeatureText;
        private set => SetProperty(ref _selectedListingFeatureText, value);
    }

    public string SelectedListingExternalUrl
    {
        get => _selectedListingExternalUrl;
        private set => SetProperty(ref _selectedListingExternalUrl, value);
    }

    public string SelectedListingImageUrl
    {
        get => _selectedListingImageUrl;
        private set => SetProperty(ref _selectedListingImageUrl, value);
    }

    public BitmapImage? SelectedListingImageSource
    {
        get => _selectedListingImageSource;
        private set => SetProperty(ref _selectedListingImageSource, value);
    }

    public bool CanOpenSelectedListingUrl => !string.IsNullOrWhiteSpace(SelectedListingExternalUrl);

    public bool CanOpenSelectedImageUrl => !string.IsNullOrWhiteSpace(SelectedListingImageUrl);

    public string RoiInitialCashText
    {
        get => _roiInitialCashText;
        private set => SetProperty(ref _roiInitialCashText, value);
    }

    public string RoiLoanPaymentText
    {
        get => _roiLoanPaymentText;
        private set => SetProperty(ref _roiLoanPaymentText, value);
    }

    public string RoiTuroFeesText
    {
        get => _roiTuroFeesText;
        private set => SetProperty(ref _roiTuroFeesText, value);
    }

    public string RoiOperatingCostsText
    {
        get => _roiOperatingCostsText;
        private set => SetProperty(ref _roiOperatingCostsText, value);
    }

    public string RoiNetCashFlowText
    {
        get => _roiNetCashFlowText;
        private set => SetProperty(ref _roiNetCashFlowText, value);
    }

    public string RoiBreakEvenRevenueText
    {
        get => _roiBreakEvenRevenueText;
        private set => SetProperty(ref _roiBreakEvenRevenueText, value);
    }

    public string RoiBreakEvenUtilizationText
    {
        get => _roiBreakEvenUtilizationText;
        private set => SetProperty(ref _roiBreakEvenUtilizationText, value);
    }

    public string RoiAnnualYieldText
    {
        get => _roiAnnualYieldText;
        private set => SetProperty(ref _roiAnnualYieldText, value);
    }

    public string RoiCashOnCashText
    {
        get => _roiCashOnCashText;
        private set => SetProperty(ref _roiCashOnCashText, value);
    }

    public string RoiSummaryText
    {
        get => _roiSummaryText;
        private set => SetProperty(ref _roiSummaryText, value);
    }

    public string ComparableSummaryText
    {
        get => _comparableSummaryText;
        private set => SetProperty(ref _comparableSummaryText, value);
    }

    public bool HasData => _allListings.Count > 0;

    public void LoadFiles(IEnumerable<string> filePaths)
    {
        var cleanFilePaths = filePaths
            .Where(path => path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleanFilePaths.Count == 0)
        {
            StatusMessage = "No CSV files were selected.";
            return;
        }

        var importResult = _csvImportService.LoadFiles(cleanFilePaths);
        MergeImportResult(importResult);
    }

    public void LoadMappedFiles(IEnumerable<(string FilePath, ImportMapping Mapping)> fileMappings)
    {
        var confirmedMappings = fileMappings
            .Where(file => file.FilePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            .DistinctBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (confirmedMappings.Count == 0)
        {
            StatusMessage = "No CSV files were selected.";
            return;
        }

        var importResult = _csvImportService.LoadFiles(confirmedMappings);
        MergeImportResult(importResult);
    }

    private void MergeImportResult(ListingImportResult importResult)
    {
        var incomingSourceFiles = importResult.Listings
            .Select(listing => listing.SourceFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _allListings = _allListings
            .Where(listing => !incomingSourceFiles.Contains(listing.SourceFile))
            .Concat(importResult.Listings)
            .ToList();

        foreach (var warning in importResult.Warnings)
        {
            if (!_warningMessages.Contains(warning, StringComparer.OrdinalIgnoreCase))
            {
                _warningMessages.Add(warning);
            }
        }

        try
        {
            _knowledgeBaseService.UpsertListings(importResult.Listings);
            ReloadKnowledgeBase();
        }
        catch (Exception ex)
        {
            _warningMessages.Add($"Knowledge base update skipped: {ex.Message}");
            UpdateKnowledgeBaseTexts();
        }

        RefreshFilterOptions();
        ApplyFilters();
        OnPropertyChanged(nameof(HasData));
    }

    public void ClearSession()
    {
        _allListings = [];
        _filteredListings = [];
        _warningMessages.Clear();

        _suppressFilterRefresh = true;
        AvailableMarkets.Clear();
        AvailableMarkets.Add(AllMarketsLabel);
        AvailableSources.Clear();
        AvailableSources.Add(AllSourcesLabel);
        SelectedMarket = AllMarketsLabel;
        SelectedSource = AllSourcesLabel;
        _suppressFilterRefresh = false;

        _suppressSelectionAnalysisRefresh = true;
        SelectedListing = null;
        _suppressSelectionAnalysisRefresh = false;

        TopModels.Clear();
        TopVehicleTypes.Clear();
        TopMarkets.Clear();
        MarketGaps.Clear();
        TopHosts.Clear();
        SeasonalityInsights.Clear();
        ComparableVehicles.Clear();
        SelectedListingRevenueHistory.Clear();
        VisibleListings.Clear();
        RecommendedListings.Clear();
        NarrativeInsights.Clear();

        NarrativeInsights.Add("Import one or more CSV files to generate vehicle-type, host, and model-level performance rankings.");
        NarrativeInsights.Add("This session is now clear. You can load sample files from the SampleData folder or drop your own exports into the app.");

        ListingCountText = "0";
        MarketCountText = "0";
        SourceCountText = "0";
        AverageDailyPriceText = "n/a";
        AverageUtilizationText = "n/a";
        AverageMonthlyRevenueText = "n/a";
        BestVehicleTypeText = "Need data";
        RecommendedModelText = "Need data";
        RecommendedCandidateText = "Need data";
        RecommendedCandidateScoreText = "n/a";
        RecommendedCandidateReasonText = "Recommendations will appear here once the app can compare segment strength and listing-level demand.";
        RecommendationHeadlineText = "Import data to generate a car recommendation.";
        ImportSummary = BuildImportSummary(0, 0);
        ImportNotesText = "The importer accepts CSV exports from tools like Raptor Explorer and RentScout, then maps their columns into one common analysis model.";
        StatusMessage = "Session cleared. Import CSV files to start again. The local knowledge base stays intact.";
        _activeCurrencySymbol = "$";

        UpdateKnowledgeBaseTexts();

        ClearSelectionOutputs();
        ResetInvestmentDefaults();
        OnPropertyChanged(nameof(HasData));
    }

    public void ResetInvestmentDefaults()
    {
        _suppressSelectionAnalysisRefresh = true;
        PurchasePriceInput = "35000";
        DownPaymentInput = "7000";
        LoanAprInput = "7.9";
        LoanTermMonthsInput = "60";
        InsurancePerMonthInput = "180";
        MaintenancePerMonthInput = "95";
        CleaningPerMonthInput = "80";
        OtherFixedCostsInput = "40";
        TuroFeePercentInput = "25";
        _suppressSelectionAnalysisRefresh = false;
        RefreshSelectionAnalysis();
    }

    private void RefreshFilterOptions()
    {
        var currentMarket = SelectedMarket;
        var currentSource = SelectedSource;

        _suppressFilterRefresh = true;

        AvailableMarkets.Clear();
        AvailableMarkets.Add(AllMarketsLabel);
        foreach (var market in _allListings
                     .Select(listing => listing.Market)
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value))
        {
            AvailableMarkets.Add(market);
        }

        AvailableSources.Clear();
        AvailableSources.Add(AllSourcesLabel);
        foreach (var source in _allListings
                     .Select(listing => listing.SourceName)
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value))
        {
            AvailableSources.Add(source);
        }

        _selectedMarket = AvailableMarkets.Contains(currentMarket) ? currentMarket : AllMarketsLabel;
        _selectedSource = AvailableSources.Contains(currentSource) ? currentSource : AllSourcesLabel;

        OnPropertyChanged(nameof(SelectedMarket));
        OnPropertyChanged(nameof(SelectedSource));

        _suppressFilterRefresh = false;
    }

    private void ApplyFilters()
    {
        _filteredListings = _allListings
            .Where(listing => SelectedMarket == AllMarketsLabel || listing.Market.Equals(SelectedMarket, StringComparison.OrdinalIgnoreCase))
            .Where(listing => SelectedSource == AllSourcesLabel || listing.SourceName.Equals(SelectedSource, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var snapshot = _analysisEngine.CreateSnapshot(_filteredListings, _knowledgeBaseListings);
        var knowledgeBaseSnapshot = _knowledgeBaseListings.Count == 0
            ? null
            : _analysisEngine.CreateSnapshot(_knowledgeBaseListings);

        _activeCurrencySymbol = snapshot.Summary.CurrencySymbol;

        ResetCollection(TopModels, snapshot.TopModels);
        ResetCollection(TopVehicleTypes, snapshot.TopVehicleTypes);
        ResetCollection(TopMarkets, snapshot.TopMarkets);
        ResetCollection(MarketGaps, snapshot.MarketGaps);
        ResetCollection(TopHosts, snapshot.TopHosts);
        ResetCollection(SeasonalityInsights, snapshot.SeasonalityInsights);
        ResetCollection(VisibleListings, snapshot.Listings);
        ResetCollection(RecommendedListings, snapshot.RecommendedListings);
        ResetCollection(NarrativeInsights, snapshot.NarrativeInsights);

        ListingCountText = snapshot.Summary.ListingCount.ToString("N0", CultureInfo.InvariantCulture);
        MarketCountText = snapshot.Summary.MarketCount.ToString("N0", CultureInfo.InvariantCulture);
        SourceCountText = snapshot.Summary.SourceCount.ToString("N0", CultureInfo.InvariantCulture);
        AverageDailyPriceText = FormatCurrency(snapshot.Summary.AverageDailyPrice);
        AverageUtilizationText = FormatPercent(snapshot.Summary.AverageUtilization);
        AverageMonthlyRevenueText = FormatCurrency(snapshot.Summary.AverageMonthlyRevenue);
        BestVehicleTypeText = snapshot.Summary.BestVehicleType;
        RecommendedModelText = snapshot.Recommendation.RecommendedModel;
        RecommendedCandidateText = snapshot.Recommendation.RecommendedCandidate;
        RecommendedCandidateScoreText = snapshot.Recommendation.RecommendedCandidateScoreText;
        RecommendedCandidateReasonText = snapshot.Recommendation.RecommendationReason;
        RecommendationHeadlineText = snapshot.Recommendation.Headline;

        var totalFiles = _allListings
            .Select(listing => listing.SourceFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        ImportSummary = BuildImportSummary(totalFiles, _allListings.Count);

        ImportNotesText = BuildImportNotes();
        UpdateKnowledgeBaseTexts(snapshot, knowledgeBaseSnapshot);
        SyncSelectedListingWithCurrentFilter();

        if (_filteredListings.Count == 0)
        {
            StatusMessage = HasData
                ? "The current filters returned no rows. Try widening the market or source filter."
                : "Import CSV files to start profiling the market.";
            return;
        }

        var filterMessage = SelectedMarket == AllMarketsLabel && SelectedSource == AllSourcesLabel
            ? "Showing the full imported market view."
            : $"Filtered view: {SelectedMarket} | {SelectedSource}.";

        StatusMessage = $"{filterMessage} {_filteredListings.Count:N0} listings are included in the scorecard.";
    }

    private void SyncSelectedListingWithCurrentFilter()
    {
        var retainedListing = _selectedListing is null
            ? null
            : _filteredListings.FirstOrDefault(listing => IsSameListing(listing, _selectedListing));

        var recommendedListing = RecommendedListings.FirstOrDefault()?.Listing;
        var nextSelection = retainedListing ?? recommendedListing ?? _filteredListings.FirstOrDefault();

        _suppressSelectionAnalysisRefresh = true;
        SelectedListing = nextSelection;
        _suppressSelectionAnalysisRefresh = false;
        RefreshSelectionAnalysis();
    }

    private void RefreshSelectionAnalysis()
    {
        if (_suppressSelectionAnalysisRefresh)
        {
            return;
        }

        if (SelectedListing is null)
        {
            ClearSelectionOutputs();
            return;
        }

        var scenario = BuildScenario(out var scenarioNotes);
        var projection = _investmentAnalysisService.CreateProjection(SelectedListing, scenario);
        var comparables = _investmentAnalysisService.FindComparables(SelectedListing, _filteredListings);

        ResetCollection(ComparableVehicles, comparables);
        ResetCollection(SelectedListingRevenueHistory, SelectedListing.MonthlyRevenueHistory);

        SelectedListingTitleText = SelectedListing.DisplayName;
        SelectedListingContextText = $"{SelectedListing.VehicleType} | {SelectedListing.Market} | Source: {SelectedListing.SourceName}";
        SelectedListingRevenueText = FormatCurrency(SelectedListing.EstimatedMonthlyRevenue, SelectedListing.CurrencySymbol);
        SelectedListingDailyPriceText = FormatCurrency(SelectedListing.DailyPrice, SelectedListing.CurrencySymbol);
        SelectedListingUtilizationText = FormatPercent(SelectedListing.UtilizationRate);
        SelectedListingHostText = BuildSelectedListingHostText(SelectedListing);
        SelectedListingListingMetaText = BuildSelectedListingMetaText(SelectedListing);
        SelectedListingTrendText = BuildSelectedListingTrendText(SelectedListing);
        SelectedListingDemandText = BuildSelectedListingDemandText(SelectedListing);
        SelectedListingConvenienceText = BuildSelectedListingConvenienceText(SelectedListing);
        SelectedListingDiscountText = BuildSelectedListingDiscountText(SelectedListing);
        SelectedListingHostQualityText = BuildSelectedListingHostQualityText(SelectedListing);
        SelectedListingHistorySummaryText = BuildSelectedListingHistorySummaryText(SelectedListing);
        SelectedListingSparklineText = BuildSelectedListingSparklineText(SelectedListing);
        SelectedListingFeatureText = BuildSelectedListingFeatureText(SelectedListing);
        SelectedListingExternalUrl = SelectedListing.ListingUrl;
        SelectedListingImageUrl = SelectedListing.ImageUrl;
        // CSV URLs are untrusted. Images open only after explicit confirmation in the browser.
        SelectedListingImageSource = null;
        OnPropertyChanged(nameof(CanOpenSelectedListingUrl));
        OnPropertyChanged(nameof(CanOpenSelectedImageUrl));

        RoiInitialCashText = projection.HasEstimate ? FormatCurrency(projection.InitialCashRequired, SelectedListing.CurrencySymbol) : "n/a";
        RoiLoanPaymentText = projection.HasEstimate ? FormatCurrency(projection.MonthlyLoanPayment, SelectedListing.CurrencySymbol) : "n/a";
        RoiTuroFeesText = projection.HasEstimate ? FormatCurrency(projection.MonthlyTuroFees, SelectedListing.CurrencySymbol) : "n/a";
        RoiOperatingCostsText = projection.HasEstimate ? FormatCurrency(projection.MonthlyOperatingCosts, SelectedListing.CurrencySymbol) : "n/a";
        RoiNetCashFlowText = projection.HasEstimate ? FormatCurrency(projection.MonthlyNetCashFlow, SelectedListing.CurrencySymbol) : "n/a";
        RoiBreakEvenRevenueText = projection.HasEstimate ? FormatCurrency(projection.BreakEvenMonthlyRevenue, SelectedListing.CurrencySymbol) : "n/a";
        RoiBreakEvenUtilizationText = projection.HasEstimate ? FormatPercent(projection.BreakEvenUtilization) : "n/a";
        RoiAnnualYieldText = projection.HasEstimate ? FormatPercent(projection.AnnualYieldOnPurchase) : "n/a";
        RoiCashOnCashText = projection.HasEstimate ? FormatPercent(projection.CashOnCashReturn) : "n/a";

        var scenarioNote = scenarioNotes.Count == 0
            ? string.Empty
            : $" Assumption note: {string.Join(" ", scenarioNotes.Take(2))}";

        RoiSummaryText = $"{projection.Summary}{scenarioNote}".Trim();
        ComparableSummaryText = comparables.Count == 0
            ? "No close comparables were found in the current filtered view. Widen the market/source filters or import more data."
            : $"Showing {comparables.Count:N0} closest comparables for {SelectedListing.DisplayName}.";
        SyncSelectedRecommendation();
    }

    private RoiScenario BuildScenario(out List<string> notes)
    {
        notes = [];

        var purchasePrice = ParseDecimalInput(PurchasePriceInput, 35000m, "Purchase price", notes);
        var downPayment = ParseDecimalInput(DownPaymentInput, 7000m, "Down payment", notes);
        var loanApr = ParseDoubleInput(LoanAprInput, 7.9d, "APR", notes);
        var loanTermMonths = ParseIntInput(LoanTermMonthsInput, 60, "Loan term", notes);
        var insurancePerMonth = ParseDecimalInput(InsurancePerMonthInput, 180m, "Insurance", notes);
        var maintenancePerMonth = ParseDecimalInput(MaintenancePerMonthInput, 95m, "Maintenance", notes);
        var cleaningPerMonth = ParseDecimalInput(CleaningPerMonthInput, 80m, "Cleaning", notes);
        var otherFixedCosts = ParseDecimalInput(OtherFixedCostsInput, 40m, "Other costs", notes);
        var turoFeePercent = ParseDoubleInput(TuroFeePercentInput, 25d, "Turo fee", notes);

        purchasePrice = Math.Max(0m, purchasePrice);
        downPayment = Math.Max(0m, downPayment);
        if (purchasePrice > 0m && downPayment > purchasePrice)
        {
            downPayment = purchasePrice;
            notes.Add("Down payment was capped at the purchase price.");
        }

        loanApr = Math.Clamp(loanApr, 0d, 40d);
        loanTermMonths = Math.Max(0, loanTermMonths);
        turoFeePercent = Math.Clamp(turoFeePercent, 0d, 90d);

        return new RoiScenario
        {
            PurchasePrice = purchasePrice,
            DownPayment = downPayment,
            LoanAprPercent = loanApr,
            LoanTermMonths = loanTermMonths,
            InsurancePerMonth = Math.Max(0m, insurancePerMonth),
            MaintenancePerMonth = Math.Max(0m, maintenancePerMonth),
            CleaningPerMonth = Math.Max(0m, cleaningPerMonth),
            OtherFixedCostsPerMonth = Math.Max(0m, otherFixedCosts),
            TuroFeePercent = turoFeePercent,
        };
    }

    private string BuildSelectedListingHostText(TuroListing listing)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(listing.HostLabel))
        {
            parts.Add(listing.HostLabel);
        }

        if (listing.Rating.HasValue)
        {
            parts.Add($"{listing.Rating.Value:N1} rating");
        }

        if (listing.Reviews.HasValue)
        {
            parts.Add($"{listing.Reviews.Value:N0} reviews");
        }

        if (listing.Favorites.HasValue)
        {
            parts.Add($"{listing.Favorites.Value:N0} favorites");
        }

        return parts.Count == 0
            ? "Host details were not included in this import."
            : string.Join(" | ", parts);
    }

    private string BuildSelectedListingMetaText(TuroListing listing)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(listing.Trim))
        {
            parts.Add($"Trim: {listing.Trim}");
        }

        if (!string.IsNullOrWhiteSpace(listing.Transmission))
        {
            parts.Add(listing.Transmission);
        }

        if (listing.DaysOnTuro.HasValue)
        {
            parts.Add($"{listing.DaysOnTuro.Value:N0} days on Turo");
        }

        if (listing.ListingDate.HasValue)
        {
            parts.Add($"Listed {listing.ListingDate.Value:dd MMM yyyy}");
        }

        if (listing.ProtectionPlanRate.HasValue)
        {
            parts.Add($"{listing.ProtectionPlanRate.Value:P0} protection plan");
        }

        return parts.Count == 0
            ? "Maturity and setup details were not included in this import."
            : string.Join(" | ", parts);
    }

    private string BuildSelectedListingTrendText(TuroListing listing)
    {
        var historyValues = listing.MonthlyRevenueHistory
            .OrderBy(point => point.Period)
            .Select(point => point.Revenue)
            .ToList();

        if (historyValues.Count < 2)
        {
            return listing.Last12MonthRevenue.HasValue
                ? $"Trend: last 12 months total {FormatCurrency(listing.Last12MonthRevenue, listing.CurrencySymbol)}"
                : "Trend: source file did not include enough monthly history.";
        }

        var trendPercent = CalculateTrendPercent(historyValues);
        var label = trendPercent switch
        {
            >= 20d => "surging",
            >= 8d => "improving",
            <= -20d => "cooling hard",
            <= -8d => "softening",
            _ => "stable",
        };

        return $"Trend: {label} ({trendPercent:+0;-0;0}%) over the visible monthly history.";
    }

    private string BuildSelectedListingDemandText(TuroListing listing)
    {
        var utilizationComponent = (listing.UtilizationRate ?? 0d) * 45d;
        var tripsComponent = Math.Min((listing.Trips ?? 0) / 60d, 1d) * 22d;
        var reviewsComponent = Math.Min((listing.Reviews ?? 0) / 120d, 1d) * 18d;
        var favoritesComponent = Math.Min((listing.Favorites ?? 0) / 150d, 1d) * 15d;
        var score = Math.Round(utilizationComponent + tripsComponent + reviewsComponent + favoritesComponent, 0);

        return $"Demand score: {score:N0}/100 from utilization, trips, reviews, and favorites.";
    }

    private string BuildSelectedListingConvenienceText(TuroListing listing)
    {
        var airportScore = string.IsNullOrWhiteSpace(listing.AirportDelivery) ? 0d : 30d;
        var instantBookScore = string.IsNullOrWhiteSpace(listing.InstantBookLocations) ? 0d : 25d;
        var extrasScore = Math.Min(CountCsvItems(listing.Extras) * 4d, 20d);
        var badgesScore = Math.Min(CountCsvItems(listing.Badges) * 3d, 25d);
        var score = Math.Round(airportScore + instantBookScore + extrasScore + badgesScore, 0);

        return $"Convenience score: {score:N0}/100 from delivery setup, instant-book coverage, extras, and badges.";
    }

    private string BuildSelectedListingDiscountText(TuroListing listing)
    {
        if (!listing.WeeklyDiscountRate.HasValue && !listing.MonthlyDiscountRate.HasValue)
        {
            return "Discount strategy: no weekly or monthly discounts were included in this export.";
        }

        var weekly = listing.WeeklyDiscountRate.HasValue ? $"{listing.WeeklyDiscountRate.Value:P0} weekly" : "no weekly discount";
        var monthly = listing.MonthlyDiscountRate.HasValue ? $"{listing.MonthlyDiscountRate.Value:P0} monthly" : "no monthly discount";

        if (listing.MonthlyDiscountRate is >= 0.35d && listing.UtilizationRate is >= 0.65d)
        {
            return $"Discount strategy: {weekly}, {monthly}. Aggressive long-term discounting may be helping occupancy.";
        }

        return $"Discount strategy: {weekly}, {monthly}. Compare this setup against similar listings before copying it.";
    }

    private string BuildSelectedListingHostQualityText(TuroListing listing)
    {
        if (listing.HostStatus.Contains("All-Star", StringComparison.OrdinalIgnoreCase)
            && listing.Rating is >= 4.8d
            && listing.Reviews is >= 25)
        {
            return "Host quality: strong. All-Star host with review depth and a high visible rating.";
        }

        if (listing.Reviews is >= 50 && listing.Rating is >= 4.7d)
        {
            return "Host quality: solid. Strong review depth and a healthy rating.";
        }

        if (string.IsNullOrWhiteSpace(listing.HostName))
        {
            return "Host quality: host details were not included in this export.";
        }

        return "Host quality: useful to compare, but not enough visible host proof to treat as best-in-class.";
    }

    private string BuildSelectedListingHistorySummaryText(TuroListing listing)
    {
        if (listing.MonthlyRevenueHistory.Count == 0)
        {
            return listing.Last12MonthRevenue.HasValue
                ? $"Last 12 months revenue: {FormatCurrency(listing.Last12MonthRevenue, listing.CurrencySymbol)}"
                : "Monthly revenue history was not included in this source file.";
        }

        return $"{listing.MonthlyRevenueHistory.Count:N0} month(s) of revenue history loaded. Last 12 months: {FormatCurrency(listing.Last12MonthRevenue, listing.CurrencySymbol)}.";
    }

    private string BuildSelectedListingSparklineText(TuroListing listing)
    {
        var values = listing.MonthlyRevenueHistory
            .OrderBy(point => point.Period)
            .Select(point => point.Revenue)
            .ToList();

        return values.Count == 0 ? string.Empty : BuildSparkline(values);
    }

    private string BuildSelectedListingFeatureText(TuroListing listing)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(listing.AirportDelivery))
        {
            parts.Add($"Airport delivery: {listing.AirportDelivery}");
        }

        if (!string.IsNullOrWhiteSpace(listing.InstantBookLocations))
        {
            parts.Add($"Instant book: {TrimCsvList(listing.InstantBookLocations, 3)}");
        }

        if (!string.IsNullOrWhiteSpace(listing.Extras))
        {
            parts.Add($"Extras: {TrimCsvList(listing.Extras, 4)}");
        }

        if (!string.IsNullOrWhiteSpace(listing.Badges))
        {
            parts.Add($"Badges: {TrimCsvList(listing.Badges, 4)}");
        }

        return parts.Count == 0
            ? "Feature and delivery setup details were not included in this export."
            : string.Join(" | ", parts);
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
            var index = (int)Math.Round(normalized * (levels.Length - 1), MidpointRounding.AwayFromZero);
            return levels[Math.Clamp(index, 0, levels.Length - 1)];
        }));
    }

    private static int CountCsvItems(string rawText)
    {
        return string.IsNullOrWhiteSpace(rawText)
            ? 0
            : rawText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    private static string TrimCsvList(string rawText, int maxItems)
    {
        var items = rawText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (items.Length <= maxItems)
        {
            return string.Join(", ", items);
        }

        return $"{string.Join(", ", items.Take(maxItems))} +{items.Length - maxItems:N0} more";
    }

    private void SyncSelectedRecommendation()
    {
        var nextRecommendation = _selectedListing is null
            ? null
            : RecommendedListings.FirstOrDefault(recommendation => IsSameListing(recommendation.Listing, _selectedListing));

        if (!ReferenceEquals(_selectedRecommendation, nextRecommendation))
        {
            _selectedRecommendation = nextRecommendation;
            OnPropertyChanged(nameof(SelectedRecommendation));
        }
    }

    private void ReloadKnowledgeBase()
    {
        try
        {
            _knowledgeBaseListings = _knowledgeBaseService.LoadAllListings().ToList();
        }
        catch (Exception ex)
        {
            _knowledgeBaseListings = [];
            KnowledgeBaseStatusText = $"Knowledge base unavailable: {ex.Message}";
            HistoricalComparisonText = "Current recommendations are using only the active session while the local database is unavailable.";
            return;
        }

        UpdateKnowledgeBaseTexts();
    }

    private void UpdateKnowledgeBaseTexts(
        DashboardSnapshot? currentSnapshot = null,
        DashboardSnapshot? knowledgeBaseSnapshot = null)
    {
        if (_knowledgeBaseListings.Count == 0)
        {
            KnowledgeBaseStatusText = "Knowledge base is empty. Imported listings will be stored locally for future baselines.";
            HistoricalComparisonText = "Recommendations are using only the current session until the app has stored historical listings.";
            return;
        }

        var baselineSnapshot = knowledgeBaseSnapshot ?? _analysisEngine.CreateSnapshot(_knowledgeBaseListings);
        KnowledgeBaseStatusText =
            $"{_knowledgeBaseListings.Count:N0} listing profiles are stored locally across {baselineSnapshot.Summary.MarketCount:N0} markets. Historical leaders: {baselineSnapshot.Summary.BestVehicleType}, {baselineSnapshot.Recommendation.RecommendedModel}, {baselineSnapshot.Recommendation.RecommendedMarket}.";

        HistoricalComparisonText = currentSnapshot is null || currentSnapshot.Summary.ListingCount == 0
            ? "Import or filter listings to compare the active view against the stored historical baseline."
            : BuildHistoricalComparison(currentSnapshot, baselineSnapshot);
    }

    private string BuildImportSummary(int totalFiles, int totalListings)
    {
        var knowledgeBaseCount = _knowledgeBaseListings.Count;

        return totalFiles == 0
            ? $"No CSVs loaded yet. Use the import button or drop files into the window. {knowledgeBaseCount:N0} listing profile(s) are already stored in the local knowledge base."
            : $"{totalFiles:N0} file(s) loaded | {totalListings:N0} listing rows normalized | {knowledgeBaseCount:N0} listing profile(s) stored in the knowledge base";
    }

    private string BuildHistoricalComparison(DashboardSnapshot currentSnapshot, DashboardSnapshot historicalSnapshot)
    {
        var revenueComparison = DescribeDelta(
            currentSnapshot.Summary.AverageMonthlyRevenue,
            historicalSnapshot.Summary.AverageMonthlyRevenue,
            value => FormatCurrency(value));

        var utilizationComparison = DescribeDelta(
            currentSnapshot.Summary.AverageUtilization,
            historicalSnapshot.Summary.AverageUtilization,
            FormatPercent);

        return $"Current view vs stored history: revenue is {revenueComparison} and utilization is {utilizationComparison}. Historically, the strongest long-run target has been {historicalSnapshot.Recommendation.RecommendedModel} / {historicalSnapshot.Summary.BestVehicleType} in {historicalSnapshot.Recommendation.RecommendedMarket}.";
    }

    private static string DescribeDelta<T>(T? currentValue, T? baselineValue, Func<T?, string> formatter)
        where T : struct, IComparable<T>
    {
        if (!currentValue.HasValue || !baselineValue.HasValue)
        {
            return "not available";
        }

        var formattedBaseline = formatter(baselineValue);
        var comparison = currentValue.Value.CompareTo(baselineValue.Value);
        return comparison switch
        {
            > 0 => $"above the baseline ({formattedBaseline})",
            < 0 => $"below the baseline ({formattedBaseline})",
            _ => $"in line with the baseline ({formattedBaseline})",
        };
    }

    private void ClearSelectionOutputs()
    {
        ResetCollection(ComparableVehicles, Array.Empty<ComparableVehicle>());
        ResetCollection(SelectedListingRevenueHistory, Array.Empty<MonthlyRevenuePoint>());
        _selectedRecommendation = null;
        OnPropertyChanged(nameof(SelectedRecommendation));
        SelectedListingTitleText = "Choose a listing";
        SelectedListingContextText = "Select a row in the Listings tab to inspect ROI and nearby comparables.";
        SelectedListingRevenueText = "n/a";
        SelectedListingDailyPriceText = "n/a";
        SelectedListingUtilizationText = "n/a";
        SelectedListingHostText = "Host details will appear here once a listing is selected.";
        SelectedListingListingMetaText = "Pick a listing to see maturity, transmission, and market setup.";
        SelectedListingTrendText = "Trend: n/a";
        SelectedListingDemandText = "Demand score: n/a";
        SelectedListingConvenienceText = "Convenience score: n/a";
        SelectedListingDiscountText = "Discount strategy: n/a";
        SelectedListingHostQualityText = "Host quality: n/a";
        SelectedListingHistorySummaryText = "Monthly revenue history will appear here when the source file includes it.";
        SelectedListingSparklineText = string.Empty;
        SelectedListingFeatureText = "Feature and delivery setup details appear here once a candidate is selected.";
        SelectedListingExternalUrl = string.Empty;
        SelectedListingImageUrl = string.Empty;
        SelectedListingImageSource = null;
        OnPropertyChanged(nameof(CanOpenSelectedListingUrl));
        OnPropertyChanged(nameof(CanOpenSelectedImageUrl));
        RoiInitialCashText = "n/a";
        RoiLoanPaymentText = "n/a";
        RoiTuroFeesText = "n/a";
        RoiOperatingCostsText = "n/a";
        RoiNetCashFlowText = "n/a";
        RoiBreakEvenRevenueText = "n/a";
        RoiBreakEvenUtilizationText = "n/a";
        RoiAnnualYieldText = "n/a";
        RoiCashOnCashText = "n/a";
        RoiSummaryText = "Add acquisition assumptions and choose a listing to see net cash flow, yield, and break-even estimates.";
        ComparableSummaryText = "Comparables will appear here once a listing is selected.";
    }

    private string BuildImportNotes()
    {
        if (_warningMessages.Count == 0)
        {
            return $"Import note: the app now keeps richer fields like host info, discounts, delivery setup, and monthly revenue history when they are available in the source export. Imported listings are also stored in a local knowledge base at {_knowledgeBaseService.DatabasePath}.";
        }

        var lines = _warningMessages
            .Take(4)
            .ToList();

        if (_warningMessages.Count > 4)
        {
            lines.Add($"...plus {_warningMessages.Count - 4:N0} more import note(s).");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static decimal ParseDecimalInput(string rawValue, decimal fallback, string label, List<string> notes)
    {
        if (TryParseDecimal(rawValue, out var parsed))
        {
            return parsed;
        }

        notes.Add($"{label} used the default value.");
        return fallback;
    }

    private static double ParseDoubleInput(string rawValue, double fallback, string label, List<string> notes)
    {
        if (TryParseDouble(rawValue, out var parsed))
        {
            return parsed;
        }

        notes.Add($"{label} used the default value.");
        return fallback;
    }

    private static int ParseIntInput(string rawValue, int fallback, string label, List<string> notes)
    {
        if (TryParseDouble(rawValue, out var parsed))
        {
            return (int)Math.Round(parsed, MidpointRounding.AwayFromZero);
        }

        notes.Add($"{label} used the default value.");
        return fallback;
    }

    private static bool TryParseDecimal(string rawValue, out decimal parsedValue)
    {
        var sanitized = SanitizeNumeric(rawValue);
        return decimal.TryParse(
            sanitized,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out parsedValue);
    }

    private static bool TryParseDouble(string rawValue, out double parsedValue)
    {
        var sanitized = SanitizeNumeric(rawValue);
        return double.TryParse(
            sanitized,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out parsedValue);
    }

    private static string SanitizeNumeric(string rawValue)
    {
        return (rawValue ?? string.Empty)
            .Trim()
            .Replace("$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("£", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("€", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("%", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(",", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameListing(TuroListing left, TuroListing right)
    {
        if (!string.IsNullOrWhiteSpace(left.VehicleId) && !string.IsNullOrWhiteSpace(right.VehicleId))
        {
            return left.VehicleId.Equals(right.VehicleId, StringComparison.OrdinalIgnoreCase);
        }

        return left.SourceFile.Equals(right.SourceFile, StringComparison.OrdinalIgnoreCase)
            && left.DisplayName.Equals(right.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private static void ResetCollection<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values)
        {
            collection.Add(value);
        }
    }

    private string FormatCurrency(decimal? value, string? currencySymbol = null)
    {
        if (!value.HasValue)
        {
            return "n/a";
        }

        var absoluteValue = decimal.Abs(value.Value);
        var symbol = string.IsNullOrWhiteSpace(currencySymbol) ? _activeCurrencySymbol : currencySymbol!;
        var formatted = string.Create(CultureInfo.InvariantCulture, $"{symbol}{absoluteValue:N0}");
        return value.Value < 0m ? $"-{formatted}" : formatted;
    }

    private static string FormatPercent(double? value)
    {
        return value.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{value.Value:P0}")
            : "n/a";
    }
}

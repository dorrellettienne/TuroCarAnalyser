using System.Collections.ObjectModel;
using System.Windows;
using TuroCarAnalyser.Infrastructure;
using TuroCarAnalyser.Models;

namespace TuroCarAnalyser.Views;

public partial class ImportMappingWindow : Window
{
    private readonly ImportMappingWindowViewModel _viewModel;

    public ImportMapping? ConfirmedMapping { get; private set; }

    public ImportMappingWindow(string fileName, IEnumerable<string> headers, ImportMapping inferredMapping)
    {
        InitializeComponent();
        _viewModel = new ImportMappingWindowViewModel(fileName, headers, inferredMapping);
        DataContext = _viewModel;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsValid)
        {
            return;
        }

        ConfirmedMapping = _viewModel.BuildMapping();
        DialogResult = true;
        Close();
    }

    private sealed class ImportMappingWindowViewModel : ObservableObject
    {
        private const string UnmappedOption = "(Unmapped)";

        private readonly string? _autoTitleColumn;
        private readonly string? _autoMakeColumn;
        private readonly string? _autoModelColumn;
        private readonly string? _autoYearColumn;
        private readonly string? _autoDailyPriceColumn;
        private readonly string? _autoMonthlyRevenueColumn;
        private readonly string? _autoUtilizationColumn;
        private readonly string? _autoTripsColumn;
        private readonly string? _autoRatingColumn;
        private readonly string? _autoLocationColumn;

        private string _titleColumnSelection = UnmappedOption;
        private string _makeColumnSelection = UnmappedOption;
        private string _modelColumnSelection = UnmappedOption;
        private string _yearColumnSelection = UnmappedOption;
        private string _dailyPriceColumnSelection = UnmappedOption;
        private string _monthlyRevenueColumnSelection = UnmappedOption;
        private string _utilizationColumnSelection = UnmappedOption;
        private string _tripsColumnSelection = UnmappedOption;
        private string _ratingColumnSelection = UnmappedOption;
        private string _locationColumnSelection = UnmappedOption;
        private string _validationMessage = string.Empty;
        private bool _isValid;

        public ImportMappingWindowViewModel(string fileName, IEnumerable<string> headers, ImportMapping inferredMapping)
        {
            IntroText = $"Review how columns from {fileName} should map before import. Auto-detected values can be changed.";

            AvailableHeaders.Add(UnmappedOption);
            foreach (var header in headers.Where(header => !string.IsNullOrWhiteSpace(header)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                AvailableHeaders.Add(header);
            }

            _autoTitleColumn = inferredMapping.TitleColumn;
            _autoMakeColumn = inferredMapping.MakeColumn;
            _autoModelColumn = inferredMapping.ModelColumn;
            _autoYearColumn = inferredMapping.YearColumn;
            _autoDailyPriceColumn = inferredMapping.DailyPriceColumn;
            _autoMonthlyRevenueColumn = inferredMapping.MonthlyRevenueColumn;
            _autoUtilizationColumn = inferredMapping.UtilizationColumn;
            _autoTripsColumn = inferredMapping.TripsColumn;
            _autoRatingColumn = inferredMapping.RatingColumn;
            _autoLocationColumn = inferredMapping.LocationColumn;

            _titleColumnSelection = ToSelection(_autoTitleColumn);
            _makeColumnSelection = ToSelection(_autoMakeColumn);
            _modelColumnSelection = ToSelection(_autoModelColumn);
            _yearColumnSelection = ToSelection(_autoYearColumn);
            _dailyPriceColumnSelection = ToSelection(_autoDailyPriceColumn);
            _monthlyRevenueColumnSelection = ToSelection(_autoMonthlyRevenueColumn);
            _utilizationColumnSelection = ToSelection(_autoUtilizationColumn);
            _tripsColumnSelection = ToSelection(_autoTripsColumn);
            _ratingColumnSelection = ToSelection(_autoRatingColumn);
            _locationColumnSelection = ToSelection(_autoLocationColumn);

            UpdateValidation();
        }

        public ObservableCollection<string> AvailableHeaders { get; } = [];

        public string IntroText { get; }

        public string TitleColumnSelection
        {
            get => _titleColumnSelection;
            set => SetSelection(ref _titleColumnSelection, value, nameof(TitleStatusText));
        }

        public string MakeColumnSelection
        {
            get => _makeColumnSelection;
            set => SetSelection(ref _makeColumnSelection, value, nameof(MakeStatusText));
        }

        public string ModelColumnSelection
        {
            get => _modelColumnSelection;
            set => SetSelection(ref _modelColumnSelection, value, nameof(ModelStatusText));
        }

        public string YearColumnSelection
        {
            get => _yearColumnSelection;
            set => SetSelection(ref _yearColumnSelection, value, nameof(YearStatusText));
        }

        public string DailyPriceColumnSelection
        {
            get => _dailyPriceColumnSelection;
            set => SetSelection(ref _dailyPriceColumnSelection, value, nameof(DailyPriceStatusText));
        }

        public string MonthlyRevenueColumnSelection
        {
            get => _monthlyRevenueColumnSelection;
            set => SetSelection(ref _monthlyRevenueColumnSelection, value, nameof(MonthlyRevenueStatusText));
        }

        public string UtilizationColumnSelection
        {
            get => _utilizationColumnSelection;
            set => SetSelection(ref _utilizationColumnSelection, value, nameof(UtilizationStatusText));
        }

        public string TripsColumnSelection
        {
            get => _tripsColumnSelection;
            set => SetSelection(ref _tripsColumnSelection, value, nameof(TripsStatusText));
        }

        public string RatingColumnSelection
        {
            get => _ratingColumnSelection;
            set => SetSelection(ref _ratingColumnSelection, value, nameof(RatingStatusText));
        }

        public string LocationColumnSelection
        {
            get => _locationColumnSelection;
            set => SetSelection(ref _locationColumnSelection, value, nameof(LocationStatusText));
        }

        public string TitleStatusText => BuildStatusText(TitleColumnSelection, _autoTitleColumn);

        public string MakeStatusText => BuildStatusText(MakeColumnSelection, _autoMakeColumn);

        public string ModelStatusText => BuildStatusText(ModelColumnSelection, _autoModelColumn);

        public string YearStatusText => BuildStatusText(YearColumnSelection, _autoYearColumn);

        public string DailyPriceStatusText => BuildStatusText(DailyPriceColumnSelection, _autoDailyPriceColumn);

        public string MonthlyRevenueStatusText => BuildStatusText(MonthlyRevenueColumnSelection, _autoMonthlyRevenueColumn);

        public string UtilizationStatusText => BuildStatusText(UtilizationColumnSelection, _autoUtilizationColumn);

        public string TripsStatusText => BuildStatusText(TripsColumnSelection, _autoTripsColumn);

        public string RatingStatusText => BuildStatusText(RatingColumnSelection, _autoRatingColumn);

        public string LocationStatusText => BuildStatusText(LocationColumnSelection, _autoLocationColumn);

        public string ValidationMessage
        {
            get => _validationMessage;
            private set => SetProperty(ref _validationMessage, value);
        }

        public bool IsValid
        {
            get => _isValid;
            private set => SetProperty(ref _isValid, value);
        }

        public ImportMapping BuildMapping()
        {
            var mapping = new ImportMapping
            {
                TitleColumn = ToMappedColumn(TitleColumnSelection),
                MakeColumn = ToMappedColumn(MakeColumnSelection),
                ModelColumn = ToMappedColumn(ModelColumnSelection),
                YearColumn = ToMappedColumn(YearColumnSelection),
                DailyPriceColumn = ToMappedColumn(DailyPriceColumnSelection),
                MonthlyRevenueColumn = ToMappedColumn(MonthlyRevenueColumnSelection),
                UtilizationColumn = ToMappedColumn(UtilizationColumnSelection),
                TripsColumn = ToMappedColumn(TripsColumnSelection),
                RatingColumn = ToMappedColumn(RatingColumnSelection),
                LocationColumn = ToMappedColumn(LocationColumnSelection),
            };

            mapping.RawMappings = BuildRawMappings(mapping);
            return mapping;
        }

        private void SetSelection(ref string field, string? value, string statusPropertyName)
        {
            var normalizedValue = NormalizeSelection(value);
            if (!SetProperty(ref field, normalizedValue))
            {
                return;
            }

            OnPropertyChanged(statusPropertyName);
            UpdateValidation();
        }

        private void UpdateValidation()
        {
            var validationMessages = new List<string>();

            var hasFinancialField = HasMappedColumn(DailyPriceColumnSelection) || HasMappedColumn(MonthlyRevenueColumnSelection);
            if (!hasFinancialField)
            {
                validationMessages.Add("Map either Daily Price or Monthly Revenue before importing.");
            }

            var hasIdentifier = HasMappedColumn(TitleColumnSelection)
                || (HasMappedColumn(MakeColumnSelection) && HasMappedColumn(ModelColumnSelection));

            if (!hasIdentifier)
            {
                validationMessages.Add("Map either Title or both Make and Model so imported rows can be identified.");
            }

            IsValid = validationMessages.Count == 0;
            ValidationMessage = IsValid
                ? "Validation passed. Review any fields marked as User-selected, then confirm the import."
                : string.Join(" ", validationMessages);
        }

        private static string BuildStatusText(string selectedValue, string? autoDetectedValue)
        {
            if (!HasMappedColumn(selectedValue))
            {
                return "Unmapped";
            }

            if (!string.IsNullOrWhiteSpace(autoDetectedValue)
                && selectedValue.Equals(autoDetectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return "Auto-detected";
            }

            return "User-selected";
        }

        private string ToSelection(string? mappedColumn)
        {
            return !string.IsNullOrWhiteSpace(mappedColumn)
                && AvailableHeaders.Contains(mappedColumn, StringComparer.OrdinalIgnoreCase)
                ? AvailableHeaders.First(header => header.Equals(mappedColumn, StringComparison.OrdinalIgnoreCase))
                : UnmappedOption;
        }

        private static string NormalizeSelection(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? UnmappedOption : value;
        }

        private static bool HasMappedColumn(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && !value.Equals(UnmappedOption, StringComparison.OrdinalIgnoreCase);
        }

        private static string? ToMappedColumn(string selectedValue)
        {
            return HasMappedColumn(selectedValue) ? selectedValue : null;
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
    }
}

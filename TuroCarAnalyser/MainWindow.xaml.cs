using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TuroCarAnalyser.Models;
using TuroCarAnalyser.Services;
using TuroCarAnalyser.ViewModels;
using TuroCarAnalyser.Views;

namespace TuroCarAnalyser;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly CsvImportService _csvImportService = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FitWindowToWorkArea();
    }

    private void PageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var nestedScrollViewer = FindAncestor<ScrollViewer>(e.OriginalSource as DependencyObject);
        if (nestedScrollViewer is not null && !ReferenceEquals(nestedScrollViewer, scrollViewer))
        {
            return;
        }

        const double wheelStep = 48d;
        var direction = e.Delta > 0 ? -1d : 1d;
        var targetOffset = Math.Clamp(
            scrollViewer.VerticalOffset + (direction * wheelStep),
            0d,
            scrollViewer.ScrollableHeight);

        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Multiselect = true,
            Title = "Choose one or more market research CSV files",
        };

        if (dialog.ShowDialog(this) == true)
        {
            BeginMappedImport(dialog.FileNames);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.HasData)
        {
            var result = MessageBox.Show(
                this,
                "Clear the current imported session? The local knowledge base will stay intact.",
                "Clear Session",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        _viewModel.ClearSession();
    }

    private void ResetAssumptionsButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetInvestmentDefaults();
    }

    private void SessionMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.ContextMenu is { } menu)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void OpenListingButton_Click(object sender, RoutedEventArgs e)
    {
        OpenExternalUrl(_viewModel.SelectedListingExternalUrl, "listing page");
    }

    private void OpenImageButton_Click(object sender, RoutedEventArgs e)
    {
        OpenExternalUrl(_viewModel.SelectedListingImageUrl, "image");
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = HasCsvFiles(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        var files = GetCsvFiles(e.Data).ToList();
        if (files.Count > 0)
        {
            BeginMappedImport(files);
        }
    }

    private static bool HasCsvFiles(IDataObject data)
    {
        return GetCsvFiles(data).Any();
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static IEnumerable<string> GetCsvFiles(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop))
        {
            return Enumerable.Empty<string>();
        }

        var files = data.GetData(DataFormats.FileDrop) as string[];
        return files is null
            ? Enumerable.Empty<string>()
            : files.Where(path => path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
    }

    private void BeginMappedImport(IEnumerable<string> filePaths)
    {
        var confirmedMappings = new List<(string FilePath, ImportMapping Mapping)>();
        var directImportFiles = new List<string>();

        foreach (var filePath in filePaths
                     .Where(path => path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var headers = _csvImportService.GetHeaders(filePath);
                if (headers.Count == 0)
                {
                    MessageBox.Show(
                        this,
                        $"The file {Path.GetFileName(filePath)} does not appear to contain a readable header row.",
                        "Unable To Read CSV",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var inspection = _csvImportService.InspectCsv(filePath);
                if (inspection.IsKnownExportFormat)
                {
                    if (!inspection.CanImport)
                    {
                        MessageBox.Show(
                            this,
                            BuildCriticalImportMessage(filePath, inspection),
                            "Import Blocked",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    if (inspection.MissingOptionalColumns.Count > 0 || inspection.Notes.Count > 0)
                    {
                        MessageBox.Show(
                            this,
                            BuildOptionalImportMessage(filePath, inspection),
                            "Import Notes",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }

                    directImportFiles.Add(filePath);
                    continue;
                }

                var inferredMapping = _csvImportService.InferMapping(headers);
                var mappingWindow = new ImportMappingWindow(Path.GetFileName(filePath), headers, inferredMapping)
                {
                    Owner = this,
                };

                if (mappingWindow.ShowDialog() != true || mappingWindow.ConfirmedMapping is null)
                {
                    return;
                }

                confirmedMappings.Add((filePath, mappingWindow.ConfirmedMapping));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"The file {Path.GetFileName(filePath)} could not be prepared for import: {ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
        }

        if (directImportFiles.Count > 0)
        {
            _viewModel.LoadFiles(directImportFiles);
        }

        if (confirmedMappings.Count > 0)
        {
            _viewModel.LoadMappedFiles(confirmedMappings);
        }
    }

    private static string BuildCriticalImportMessage(string filePath, ImportValidationResult inspection)
    {
        var lines = new List<string>
        {
            $"{Path.GetFileName(filePath)} is missing required columns for the RentScout/Raptor-style export:",
            string.Join(Environment.NewLine, inspection.MissingCriticalColumns.Select(column => $"- {column}")),
        };

        if (inspection.DetectedMonthlyRevenueColumns.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add($"Monthly columns detected: {string.Join(", ", inspection.DetectedMonthlyRevenueColumns)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildOptionalImportMessage(string filePath, ImportValidationResult inspection)
    {
        var lines = new List<string>
        {
            $"{Path.GetFileName(filePath)} matches the RentScout/Raptor-style export profile.",
        };

        foreach (var note in inspection.Notes)
        {
            lines.Add(note);
        }

        if (inspection.MissingOptionalColumns.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Missing optional columns:");
            lines.Add(string.Join(Environment.NewLine, inspection.MissingOptionalColumns.Select(column => $"- {column}")));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void OpenExternalUrl(string? url, string label)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show(
                this,
                $"There is no {label} URL for the currently selected listing.",
                "Link Unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"The {label} could not be opened: {ex.Message}",
                "Open Link Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void FitWindowToWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        const double screenMargin = 24d;

        MaxWidth = workArea.Width;
        MaxHeight = workArea.Height;

        Width = Math.Min(Width, Math.Max(MinWidth, workArea.Width - screenMargin));
        Height = Math.Min(Height, Math.Max(MinHeight, workArea.Height - screenMargin));

        Left = workArea.Left + Math.Max((workArea.Width - Width) / 2d, 0d);
        Top = workArea.Top + Math.Max((workArea.Height - Height) / 2d, 0d);
    }
}

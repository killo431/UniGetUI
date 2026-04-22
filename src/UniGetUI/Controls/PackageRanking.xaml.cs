using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UniGetUI.Core.Logging;
using UniGetUI.Core.Tools;
using UniGetUI.Interface.Telemetry;
using UniGetUI.PackageEngine;
using UniGetUI.PackageEngine.Classes.Packages.Classes;

namespace UniGetUI.Controls;

public sealed partial class PackageRanking : UserControl
{
    private const int TopCount = 25;
    private bool _initialized;
    private CancellationTokenSource? _loadCts;

    public PackageRanking()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;

        PopulateManagerFilter();
        TelemetryHandler.ViewPackageRankings();
        // Initial LoadRankingsAsync is triggered via SelectionChanged when SelectedIndex is set to 0
    }

    private void PopulateManagerFilter()
    {
        ManagerFilter.Items.Clear();
        ManagerFilter.Items.Add(CoreTools.Translate("All managers"));
        foreach (var manager in PEInterface.Managers)
        {
            if (manager.IsEnabled() && manager.Status.Found)
                ManagerFilter.Items.Add(manager.Name);
        }

        ManagerFilter.SelectedIndex = 0;
    }

    private void ManagerFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        _ = LoadRankingsAsync(_loadCts.Token);
    }

    private async Task LoadRankingsAsync(CancellationToken cancellationToken = default)
    {
        LoadingRing.Visibility = Visibility.Visible;
        LoadingRing.IsActive = true;
        ErrorText.Visibility = Visibility.Collapsed;

        // Remove previously generated package rows (keep the loading ring and error text)
        while (PackagesPanel.Children.Count > 2)
            PackagesPanel.Children.RemoveAt(PackagesPanel.Children.Count - 1);

        try
        {
            var downloadsByKey = await CoreTools.FetchPackageRankingsAsync();

            if (cancellationToken.IsCancellationRequested)
                return;

            foreach (var kv in downloadsByKey)
            {
                string[] keyParts = kv.Key.Split('\\', 2);
                if (keyParts.Length == 2)
                    PackageCacher.SetDownloadCount(keyParts[0], keyParts[1], kv.Value);
            }

            string? filterManagerName = ManagerFilter.SelectedIndex > 0
                ? ManagerFilter.SelectedItem as string
                : null;

            var sorted = downloadsByKey
                .Where(kv =>
                    filterManagerName is null
                    || kv.Key.StartsWith(filterManagerName + "\\", StringComparison.Ordinal))
                .OrderByDescending(kv => kv.Value)
                .Take(TopCount)
                .ToList();

            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;

            if (sorted.Count == 0)
            {
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            int rank = 1;
            foreach (var (key, count) in sorted)
                PackagesPanel.Children.Add(BuildPackageRow(rank++, key, count));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.Error("[PackageRanking] Failed to load rankings");
            Logger.Error(ex);
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private static UIElement BuildPackageRow(int rank, string key, long downloads)
    {
        string[] keyComponents = key.Split('\\', 2);
        string managerName = keyComponents.Length > 1 ? keyComponents[0] : "?";
        string packageId = keyComponents.Length > 1 ? keyComponents[1] : key;

        Grid packageRowGrid = new()
        {
            Padding = new Thickness(8, 6, 8, 6),
            CornerRadius = new CornerRadius(4),
        };
        packageRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        packageRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        packageRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        TextBlock rankText = new()
        {
            Text = rank.ToString(),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Opacity = 0.5,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(rankText, 0);
        packageRowGrid.Children.Add(rankText);

        StackPanel packageInfoPanel = new() { VerticalAlignment = VerticalAlignment.Center };
        packageInfoPanel.Children.Add(new TextBlock
        {
            Text = CoreTools.FormatAsName(packageId),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        packageInfoPanel.Children.Add(new TextBlock
        {
            Text = $"{packageId}  ·  {managerName}",
            FontSize = 11,
            Opacity = 0.6,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(packageInfoPanel, 1);
        packageRowGrid.Children.Add(packageInfoPanel);

        StackPanel downloadsBadge = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Opacity = 0.7,
        };
        downloadsBadge.Children.Add(new FontIcon
        {
            Glyph = "\uE896",
            FontSize = 12,
            FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
        });
        downloadsBadge.Children.Add(new TextBlock
        {
            Text = CoreTools.FormatDownloadCount(downloads),
            FontSize = 12,
        });
        Grid.SetColumn(downloadsBadge, 2);
        packageRowGrid.Children.Add(downloadsBadge);

        return packageRowGrid;
    }
}

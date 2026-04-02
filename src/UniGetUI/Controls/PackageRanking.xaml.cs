using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UniGetUI.Core.Logging;
using UniGetUI.Core.Tools;
using UniGetUI.Interface.Telemetry;
using UniGetUI.PackageEngine;
using UniGetUI.PackageEngine.Classes.Packages.Classes;

namespace UniGetUI.Controls;

public sealed partial class PackageRanking : UserControl
{
    private const int TopCount = 25;

    public PackageRanking()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PopulateManagerFilter();
        _ = LoadRankingsAsync();
        TelemetryHandler.ViewPackageRankings();
    }

    private void PopulateManagerFilter()
    {
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
        _ = LoadRankingsAsync();
    }

    private async Task LoadRankingsAsync()
    {
        LoadingRing.Visibility = Visibility.Visible;
        LoadingRing.IsActive = true;
        ErrorText.Visibility = Visibility.Collapsed;

        // Remove previously generated package rows (keep the loading ring and error text)
        while (PackagesPanel.Children.Count > 2)
            PackagesPanel.Children.RemoveAt(PackagesPanel.Children.Count - 1);

        try
        {
            var rankings = await CoreTools.FetchPackageRankingsAsync();

            foreach (var kv in rankings)
            {
                string[] parts = kv.Key.Split('\\', 2);
                if (parts.Length == 2)
                    PackageCacher.SetDownloadCount(parts[0], parts[1], kv.Value);
            }

            string? filterManagerName = ManagerFilter.SelectedIndex > 0
                ? ManagerFilter.SelectedItem as string
                : null;

            var sorted = rankings
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
        catch (Exception ex)
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
        string[] parts = key.Split('\\', 2);
        string managerName = parts.Length > 1 ? parts[0] : "?";
        string packageId = parts.Length > 1 ? parts[1] : key;

        Grid row = new()
        {
            Padding = new Thickness(8, 6, 8, 6),
            CornerRadius = new CornerRadius(4),
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        TextBlock rankText = new()
        {
            Text = rank.ToString(),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Opacity = 0.5,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(rankText, 0);
        row.Children.Add(rankText);

        StackPanel info = new() { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock
        {
            Text = CoreTools.FormatAsName(packageId),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        info.Children.Add(new TextBlock
        {
            Text = $"{packageId}  ·  {managerName}",
            FontSize = 11,
            Opacity = 0.6,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(info, 1);
        row.Children.Add(info);

        TextBlock downloadsBadge = new()
        {
            Text = $"\uE896 {CoreTools.FormatDownloadCount(downloads)}",
            FontSize = 12,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        Grid.SetColumn(downloadsBadge, 2);
        row.Children.Add(downloadsBadge);

        return row;
    }
}

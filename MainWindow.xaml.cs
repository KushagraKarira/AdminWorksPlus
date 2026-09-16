using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AdminWorks.Models;
using AdminWorks.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace AdminWorks
{
    public sealed partial class MainWindow : Window
    {
        public ExecutionService ExecutionSvc { get; }
        public TelemetryService TelemetrySvc { get; }
        public ProfileService ProfileSvc { get; }

        private readonly List<TweakItem> _allTweaks;
        private string _currentCategory = "Maintenance";

        public MainWindow()
        {
            this.InitializeComponent();

            // 1. Configure Native Windows 11 TitleBar with Snap Layouts
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            // 2. Initialize Services
            ProfileSvc = new ProfileService();
            ExecutionSvc = new ExecutionService(DispatcherQueue);
            TelemetrySvc = new TelemetryService();

            // 3. Load Tweaks Catalog
            _allTweaks = TweakCatalog.GetTweaks(ProfileSvc);

            // 4. Initial status sync for toggles
            Task.Run(() =>
            {
                foreach (var tweak in _allTweaks.Where(t => t.IsToggle && t.CheckStatus != null))
                {
                    try
                    {
                        var status = tweak.CheckStatus!();
                        DispatcherQueue.TryEnqueue(() => tweak.IsActive = status);
                    }
                    catch { }
                }
            });

            // 5. Wire Telemetry Updates
            TelemetrySvc.SnapshotUpdated += OnSnapshotUpdated;

            // 6. Set System Badge
            SystemBadgeText.Text = $"{Environment.MachineName} • Windows 11 (Build {Environment.OSVersion.Version.Build})";

            // 7. Wire GitHub Auto-Updater
            var updateSvc = new UpdateService();
            updateSvc.UpdateAvailable += (tag, url) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateBadgeText.Text = tag.Length <= 8 ? tag : "UPDATE";
                    UpdateBadge.Visibility = Visibility.Visible;
                    ExecutionSvc.Log($"New update available on GitHub: {tag}", LogLevel.Success);
                });
            };
            Task.Run(updateSvc.CheckForUpdateAsync);

            // 8. Keyboard Shortcuts
            if (this.Content is UIElement uiRoot)
            {
                uiRoot.KeyDown += (s, e) =>
                {
                    var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                    if (ctrl && e.Key == Windows.System.VirtualKey.F)
                    {
                        SearchBox.Focus(FocusState.Programmatic);
                        e.Handled = true;
                    }
                    else if (ctrl && e.Key == Windows.System.VirtualKey.L)
                    {
                        ConsoleClear_Click(this, null!);
                        e.Handled = true;
                    }
                    else if (ctrl && (e.Key == Windows.System.VirtualKey.J || (int)e.Key == 192))
                    {
                        ToggleDrawer_Click(this, null!);
                        e.Handled = true;
                    }
                };
            }

            // 9. Initial View
            RefreshCards();
            ExecutionSvc.Log("AdminWorks Pro C# (Native Windows 11) initialized and ready.", LogLevel.Success);
        }

        private void UpdateBadge_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/KushagraKarira/AdminWorks/releases") { UseShellExecute = true });
        }

        private void OnSnapshotUpdated(TelemetrySnapshot snapshot)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                CpuText.Text = $"{snapshot.CpuLoad}% Utilization";
                CpuBar.Value = snapshot.CpuLoad;

                RamText.Text = $"{snapshot.RamUsedGb} / {snapshot.RamTotalGb} GB";
                RamBar.Value = snapshot.RamPercentage;

                DiskText.Text = $"{snapshot.DiskFreeGb} GB Free ({snapshot.DiskTotalGb} GB)";
                DiskBar.Value = snapshot.DiskPercentage;

                UptimeText.Text = $"{snapshot.Uptime.Days}d {snapshot.Uptime.Hours}h {snapshot.Uptime.Minutes}m";
            });
        }

        private void RefreshCards()
        {
            var query = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                CardsContainer.ItemsSource = _allTweaks.Where(t => t.Category.Equals(_currentCategory, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else
            {
                CardsContainer.ItemsSource = _allTweaks.Where(t =>
                    t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    t.CategoryTag.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string cat)
            {
                _currentCategory = cat;
                SearchBox.Text = string.Empty;
                RefreshCards();
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            RefreshCards();
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TweakItem item)
            {
                await ExecutionSvc.ExecuteTweakAsync(item);
            }
        }

        private async void TweakToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch ts && ts.DataContext is TweakItem item)
            {
                if (item.IsActive != ts.IsOn)
                {
                    await ExecutionSvc.ExecuteTweakAsync(item, ts.IsOn);
                }
            }
        }

        private void ToggleDrawer_Click(object sender, RoutedEventArgs e)
        {
            if (ConsoleContentRow.Height.Value > 0)
            {
                ConsoleContentRow.Height = new GridLength(0);
                ToggleDrawerBtn.Content = "Expand";
            }
            else
            {
                ConsoleContentRow.Height = new GridLength(140);
                ToggleDrawerBtn.Content = "Collapse";
            }
        }

        private void ConsoleClear_Click(object sender, RoutedEventArgs e)
        {
            ExecutionSvc.Logs.Clear();
            ExecutionSvc.Log("Console cleared.", LogLevel.Info);
        }

        private void ConsoleCopy_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Join(Environment.NewLine, ExecutionSvc.Logs.Select(l => $"{l.Header} {l.Message}"));
            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);
            ExecutionSvc.Log("Console output copied to clipboard.", LogLevel.Success);
        }

        private async void ConsoleExport_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Join(Environment.NewLine, ExecutionSvc.Logs.Select(l => $"{l.Header} {l.Message}"));
            var desk = ProfileService.GetDesktopPath();
            var path = Path.Combine(desk, $"AdminWorks_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            await File.WriteAllTextAsync(path, text);
            ExecutionSvc.Log($"Log exported to: {path}", LogLevel.Success);
        }
    }
}

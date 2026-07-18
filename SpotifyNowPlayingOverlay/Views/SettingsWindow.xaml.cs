using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SpotifyNowPlayingOverlay.ViewModels;
using SpotifyNowPlayingOverlay.Services;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace SpotifyNowPlayingOverlay.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void QuickColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hexColor && DataContext is SettingsViewModel vm)
            {
                vm.AccentColor = hexColor;
            }
        }

        // ═══ NAV TAB HANDLERS ═══

        private void SetActiveTab(string tab)
        {
            // Reset all nav buttons to inactive style
            NavSettingsBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x28, 0x28, 0x28));
            NavSettingsBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));
            NavAboutBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x28, 0x28, 0x28));
            NavAboutBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));
            NavUpdateBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x28, 0x28, 0x28));
            NavUpdateBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));

            // Hide all panels
            PanelSettings.Visibility = Visibility.Collapsed;
            PanelAbout.Visibility = Visibility.Collapsed;
            PanelUpdate.Visibility = Visibility.Collapsed;
            FooterSettings.Visibility = Visibility.Visible;

            // Activate selected tab
            var accentBrush = (DataContext is SettingsViewModel svm) ? svm.AccentBrush : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1D, 0xB9, 0x54));

            switch (tab)
            {
                case "Settings":
                    NavSettingsBtn.Background = accentBrush;
                    NavSettingsBtn.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);
                    PanelSettings.Visibility = Visibility.Visible;
                    FooterSettings.Visibility = Visibility.Visible;
                    break;
                case "About":
                    NavAboutBtn.Background = accentBrush;
                    NavAboutBtn.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);
                    PanelAbout.Visibility = Visibility.Visible;
                    FooterSettings.Visibility = Visibility.Collapsed;
                    break;
                case "Update":
                    NavUpdateBtn.Background = accentBrush;
                    NavUpdateBtn.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);
                    PanelUpdate.Visibility = Visibility.Visible;
                    FooterSettings.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void NavSettings_Click(object sender, RoutedEventArgs e) => SetActiveTab("Settings");
        private void NavAbout_Click(object sender, RoutedEventArgs e) => SetActiveTab("About");
        private void NavUpdate_Click(object sender, RoutedEventArgs e) => SetActiveTab("Update");

        private void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/search?q=MusicTrack+overlay&type=repositories",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                // Force bindings update just in case
                var focusedElement = System.Windows.Input.FocusManager.GetFocusedElement(this);
                if (focusedElement is TextBox tb)
                {
                    var binding = tb.GetBindingExpression(TextBox.TextProperty);
                    binding?.UpdateSource();
                }

                // Simply close settings (it saves dynamically in properties)
                Close();
            }
        }

        private async void SaveAndConnect_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                // Force bindings update just in case
                var focusedElement = System.Windows.Input.FocusManager.GetFocusedElement(this);
                if (focusedElement is TextBox tb)
                {
                    var binding = tb.GetBindingExpression(TextBox.TextProperty);
                    binding?.UpdateSource();
                }

                if (!string.IsNullOrWhiteSpace(vm.ClientId))
                {
                    // Close settings first
                    Close();

                    // Now, start the login flow asynchronously!
                    try
                    {
                        var app = (App)Application.Current;
                        var spotifyService = (ISpotifyService)app.ServiceProvider.GetService(typeof(ISpotifyService))!;
                        await spotifyService.StartLoginFlowAsync();
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Authentication Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Please enter a valid Spotify Client ID before connecting.", "Client ID Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
            base.OnClosed(e);
        }
    }
}

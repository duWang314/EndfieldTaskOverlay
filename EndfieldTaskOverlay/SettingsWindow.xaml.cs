using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace EndfieldTaskOverlay
{
    public partial class SettingsWindow : Window
    {
        private const string DefaultGamePath = "C:/Program Files/Hypergryph Launcher/games/Endfield Game/Endfield.exe";

        private readonly MainWindow _mainWindow;
        private readonly string _settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        private OverlaySettings _settings = new();

        public SettingsWindow(MainWindow mainWindow)
        {
            InitializeComponent();

            _mainWindow = mainWindow;
            Width = _mainWindow.Width;
            Height = _mainWindow.Height;
            Left = _mainWindow.Left;
            Top = _mainWindow.Top;

            LoadSettings();
            FillForm();
        }

        private void LoadSettings()
        {
            if (!File.Exists(_settingsPath))
            {
                SaveSettings();
                return;
            }

            try
            {
                var json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<OverlaySettings>(json);
                if (loaded is not null)
                {
                    _settings = loaded;
                }
            }
            catch
            {
                _settings = new OverlaySettings();
            }
        }

        private void FillForm()
        {
            AutoPinCheckBox.IsChecked = _settings.auto_pin;
            AutoLaunchGameCheckBox.IsChecked = _settings.auto_launch_game;
            GamePathTextBox.Text = _settings.game_path;
            LocationXTextBox.Text = (_settings.location_x ?? _mainWindow.Left).ToString(CultureInfo.InvariantCulture);
            LocationYTextBox.Text = (_settings.location_y ?? _mainWindow.Top).ToString(CultureInfo.InvariantCulture);
        }

        private void SaveSettings()
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_settingsPath, json);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            AutoPinCheckBox.IsChecked = true;
            AutoLaunchGameCheckBox.IsChecked = false;
            GamePathTextBox.Text = DefaultGamePath;
            LocationXTextBox.Text = Left.ToString(CultureInfo.InvariantCulture);
            LocationYTextBox.Text = Top.ToString(CultureInfo.InvariantCulture);
        }

        private void OpenJson_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{_settingsPath}\"",
                UseShellExecute = true
            });
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var updated = new OverlaySettings
            {
                auto_pin = AutoPinCheckBox.IsChecked == true,
                auto_launch_game = AutoLaunchGameCheckBox.IsChecked == true,
                game_path = (GamePathTextBox.Text ?? string.Empty).Trim().Replace('\\', '/'),
                window_width = _settings.window_width,
                window_height = _settings.window_height,
                location_x = ParseCoordinate(LocationXTextBox.Text, _mainWindow.Left),
                location_y = ParseCoordinate(LocationYTextBox.Text, _mainWindow.Top)
            };

            _settings = updated;
            SaveSettings();

            _mainWindow.ApplySettings(updated);
            Close();
        }

        private static double ParseCoordinate(string? text, double fallback)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            return fallback;
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox textBox)
            {
                e.Handled = true;
                return;
            }

            var proposed = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
                .Insert(textBox.SelectionStart, e.Text);

            e.Handled = !Regex.IsMatch(proposed, @"^-?\d*(\.\d*)?$");
        }

        private void NumericTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.SourceDataObject.GetData(DataFormats.Text) as string;
            if (string.IsNullOrWhiteSpace(text) || !Regex.IsMatch(text, @"^-?\d*(\.\d*)?$") )
            {
                e.CancelCommand();
            }
        }
    }
}

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace EndfieldTaskOverlay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string _settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        private readonly string _todoPath = Path.Combine(AppContext.BaseDirectory, "TODO.txt");
        private readonly string _readmePath = Path.Combine(AppContext.BaseDirectory, "README.pdf");
        private OverlaySettings _settings = new();
        private readonly List<string> _tasks = new();
        private readonly DispatcherTimer _taskDurationTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _shutdownTimer = new() { Interval = TimeSpan.FromSeconds(5) };
        private int _currentTaskIndex;
        private int _elapsedSeconds;
        private bool _hasStartedTiming;
        private bool _isFinishing;

        public MainWindow()
        {
            InitializeComponent();

            LoadCustomCursor();

            _taskDurationTimer.Tick += TaskDurationTimer_Tick;
            _shutdownTimer.Tick += ShutdownTimer_Tick;

            LoadSettings();
            TryLaunchGame();
            LoadTasks();
            UpdateTaskDisplay();
        }

        private void LoadCustomCursor()
        {
            try
            {
                // 获取嵌入在程序内部的光标资源流
                var streamInfo = Application.GetResourceStream(new Uri("cursor.cur", UriKind.Relative));
                if (streamInfo != null)
                {
                    // 将窗口的光标设置为我们自定义的光标
                    this.Cursor = new Cursor(streamInfo.Stream);
                }
            }
            catch (Exception)
            {
                // 保底机制：如果光标文件损坏或找不到，就静默失败，使用系统默认的箭头光标
                this.Cursor = Cursors.Arrow;
            }
        }

        private void LoadSettings()
        {
            if (!File.Exists(_settingsPath))
            {
                SaveSettings();
                Topmost = _settings.auto_pin;
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

                if (_settings.location_x.HasValue && _settings.location_y.HasValue)
                {
                    Left = _settings.location_x.Value;
                    Top = _settings.location_y.Value;
                }

                if (_settings.window_width > 0)
                {
                    Width = Math.Max(MinWidth, _settings.window_width);
                }

                if (_settings.window_height > 0)
                {
                    Height = Math.Max(MinHeight, _settings.window_height);
                }

                Topmost = _settings.auto_pin;

                if (_settings.window_opacity > 0)
                {
                    _MainWindow.Opacity = _settings.window_opacity;
                }
            }
            catch
            {
                _settings = new OverlaySettings();
                SaveSettings();
                Topmost = _settings.auto_pin;
            }
        }

        private void SaveSettings()
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_settingsPath, json);
        }

        private void TryLaunchGame()
        {
            if (!_settings.auto_launch_game || string.IsNullOrWhiteSpace(_settings.game_path) || !File.Exists(_settings.game_path))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _settings.game_path,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        private void LoadTasks()
        {
            _tasks.Clear();

            if (!File.Exists(_todoPath))
            {
                return;
            }

            foreach (var line in File.ReadLines(_todoPath))
            {
                if (line.Contains("--- 可选内容"))
                {
                    break;
                }

                var task = line.Trim();
                if (string.IsNullOrWhiteSpace(task))
                {
                    continue;
                }

                _tasks.Add(task);
            }

            if (_tasks.Count != 0 && !_hasStartedTiming)
            {
                StartTimerButton.IsEnabled = true;
            }

            _currentTaskIndex = 0;
        }

        private void UpdateTaskDisplay()
        {
            if (_tasks.Count == 0)
            {
                ProgressTextBlock.Text = "0 / 0";
                TaskTextBlock.Text = "未读取到任务，请点击\"编辑\"按钮检查或新建 TODO";
                StartTimerButton.IsEnabled = false;
                PreviousButton.IsEnabled = false;
                NextButton.IsEnabled = false;
                return;
            }

            if (_tasks.Count == 1)
            {
                NextButton.Content = "完成";
            }

            ProgressTextBlock.Text = $"{_currentTaskIndex + 1} / {_tasks.Count}";
            TaskTextBlock.Text = _tasks[_currentTaskIndex];
            PreviousButton.IsEnabled = !_isFinishing && _currentTaskIndex > 0;
            NextButton.IsEnabled = !_isFinishing;
        }

        private void TaskDurationTimer_Tick(object? sender, EventArgs e)
        {
            _elapsedSeconds++;
        }

        private void ShutdownTimer_Tick(object? sender, EventArgs e)
        {
            _shutdownTimer.Stop();
            Application.Current.Shutdown();
        }

        private void BeginFinishCountdown()
        {
            if (_isFinishing)
            {
                return;
            }

            _isFinishing = true;
            _taskDurationTimer.Stop();

            if (_hasStartedTiming)
            {
                StartTimerButton.Content = "⏳";
                var minutes = _elapsedSeconds / 60;
                var seconds = _elapsedSeconds % 60;
                TaskTextBlock.Text = $"共花费 {minutes} min {seconds} s\n比上次更快了吗？";
            }
            else
            {
                TaskTextBlock.Text = "任务全部完成！\n此小窗将于 5 秒后关闭";
            }

            PreviousButton.IsEnabled = false;
            NextButton.IsEnabled = false;
            StartTimerButton.IsEnabled = false;

            _shutdownTimer.Stop();
            _shutdownTimer.Start();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            if (e.OriginalSource is DependencyObject source && FindParent<Button>(source) is not null)
            {
                return;
            }

            if (e.OriginalSource is DependencyObject thumbSource && FindParent<Thumb>(thumbSource) is not null)
            {
                return;
            }

            try
            {
                DragMove();
                _settings.location_x = Left;
                _settings.location_y = Top;
                SaveSettings();
            }
            catch
            {
            }
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T t) return t;

                child = child switch
                {
                    Visual or Visual3D => VisualTreeHelper.GetParent(child),
                    FrameworkContentElement fce => fce.Parent,
                    ContentElement ce => ContentOperations.GetParent(ce),
                    _ => LogicalTreeHelper.GetParent(child)
                };
            }
            return null;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTaskIndex <= 0)
            {
                return;
            }

            if (_currentTaskIndex < _tasks.Count)
            {
                NextButton.Content = "下一步";
            }

            _currentTaskIndex--;
            UpdateTaskDisplay();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tasks.Count == 0 || _isFinishing)
            {
                return;
            }

            if (_currentTaskIndex >= _tasks.Count - 2)
            {
                NextButton.Content = "完成";
            }

            if (_currentTaskIndex >= _tasks.Count - 1)
            {
                BeginFinishCountdown();
                return;
            }
            
            _currentTaskIndex++;
            UpdateTaskDisplay();
        }

        private void StartTimerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hasStartedTiming)
            {
                return;
            }

            _hasStartedTiming = true;
            _elapsedSeconds = 0;
            _taskDurationTimer.Start();
            StartTimerButton.IsEnabled = false;
            StartTimerButton.Content = "⌛";
        }

        private void EditTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(_todoPath))
            {
                File.WriteAllText(_todoPath, "这是一份 TODO 样例\n您可以由此编写更适配自己的 TODO\n欢迎回家，博士！\n帝江号——收取基建产物 [I]\n帝江号——送礼物，造装备，拿日活跃奖励\n据点管理——换调度券 [Y]\n物资调度——稳定需求和弹性需求物资 [Y]\n好友——进行情报交流和生产助力\n活动——打活动（如果有）[F7]\n行动手册——清理智 [F8]\n仓储节点——送货 [Y]\n信用交易所——清信用 [F5]\n环境监测终端——拍照任务（如果有）[Y]\n采集提示——收集地图中的菌、石、叶（如果有） [M]\n亲一口洛茜\n帝江号——回到干员联络台处\n\n--- 可选内容（不全，您可以自行添加） ---\n\n此处的内容将不会出现在小窗中，如果您希望每日处理这类事务，请手动将其添加到上面\n\n武陵城——生态种植区收菜\n源石研究园——生态种植区收菜\n能量淤积点——刷材料或经验\n武陵——收集驼兽粪便\n武陵——打箱子");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{_todoPath}\"",
                UseShellExecute = true
            });

            LoadTasks();
            UpdateTaskDisplay();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(_readmePath))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = _readmePath,
                UseShellExecute = true
            });
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(this)
            {
                Owner = this
            };

            settingsWindow.ShowDialog();
        }

        public void ApplySettings(OverlaySettings settings)
        {
            _settings = settings;
            Topmost = _settings.auto_pin;

            if (_settings.location_x.HasValue)
            {
                Left = _settings.location_x.Value;
            }

            if (_settings.location_y.HasValue)
            {
                Top = _settings.location_y.Value;
            }

            if (_settings.window_width > 0)
            {
                Width = Math.Max(MinWidth, _settings.window_width);
            }

            if (_settings.window_height > 0)
            {
                Height = Math.Max(MinHeight, _settings.window_height);
            }

            if (_settings.window_opacity > 0)
            {
                Opacity = _settings.window_opacity;
            }
        }

        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_isFinishing)
            {
                return;
            }

            var newWidth = Math.Max(MinWidth, Width + e.HorizontalChange);
            var newHeight = Math.Max(MinHeight, Height + e.VerticalChange);

            Width = newWidth;
            Height = newHeight;
        }

        private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _settings.window_width = Width;
            _settings.window_height = Height;
            SaveSettings();
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            _MainWindow.Opacity = 1;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            _MainWindow.Opacity = _settings.window_opacity;

            // 【细节防错】如果当前设置子窗口处于打开状态，千万不要切焦点，否则设置窗口会失去响应或被盖住
            if (Application.Current.Windows.OfType<SettingsWindow>().Any())
            {
                return;
            }

            // 假设你的设置数据存放在名为 _settings 的实例中（请根据你的实际变量名修改）
            string gamePath = _settings.game_path;

            if (string.IsNullOrWhiteSpace(gamePath))
                return;

            // 从 "C:/.../Endfield.exe" 中提取出 "Endfield" 作为进程名
            string processName = Path.GetFileNameWithoutExtension(gamePath);

            // 在系统中查找这个进程
            Process[] processes = Process.GetProcessesByName(processName);

            if (processes.Length > 0)
            {
                // 找到游戏了！将 Windows 的活动焦点强行设置给游戏的主窗口
                SetForegroundWindow(processes[0].MainWindowHandle);
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }

    public class OverlaySettings
    {
        [JsonPropertyName("auto_pin")]
        public bool auto_pin { get; set; } = true;

        [JsonPropertyName("game_path")]
        public string game_path { get; set; } = "C:/Program Files/Hypergryph Launcher/games/Endfield Game/Endfield.exe";

        [JsonPropertyName("auto_launch_game")]
        public bool auto_launch_game { get; set; } = false;

        [JsonPropertyName("location_x")]
        public double? location_x { get; set; } = 0;

        [JsonPropertyName("location_y")]
        public double? location_y { get; set; } = 0;

        [JsonPropertyName("window_width")]
        public double window_width { get; set; } = 325;

        [JsonPropertyName("window_height")]
        public double window_height { get; set; } = 200;

        [JsonPropertyName("window_opacity")]
        public double window_opacity { get; set; } = 1;
    }
}
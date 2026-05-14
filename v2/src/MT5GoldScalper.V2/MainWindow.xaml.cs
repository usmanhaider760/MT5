using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MT5GoldScalper.V2.ViewModels;

namespace TradingDecisionSystem
{
    public partial class MainWindow : Window
    {
        // ─── layout state ───────────────────────────────────────────
        private int _columns = 8;

        // ─── light theme brush overrides ───────────────────────────
        private static readonly Dictionary<string, Color> LightColors = new()
        {
            ["Bg0"]               = Color.FromRgb(0xF0, 0xF2, 0xF8),
            ["Bg1"]               = Color.FromRgb(0xE8, 0xEA, 0xF5),
            ["Bg2"]               = Color.FromRgb(0xFF, 0xFF, 0xFF),
            ["Bg3"]               = Color.FromRgb(0xDD, 0xE0, 0xF0),
            ["Bd"]                = Color.FromRgb(0xA0, 0xA8, 0xD0),
            ["T1"]                = Color.FromRgb(0x1A, 0x1E, 0x3A),
            ["T2"]                = Color.FromRgb(0x3A, 0x40, 0x80),
            ["T3"]                = Color.FromRgb(0x55, 0x60, 0xA0),
            ["T4"]                = Color.FromRgb(0x70, 0x80, 0xB0),
            ["ColGreen"]          = Color.FromRgb(0x0A, 0x7A, 0x3A),
            ["ColYellow"]         = Color.FromRgb(0x8A, 0x5A, 0x00),
            ["ColRed"]            = Color.FromRgb(0xA0, 0x20, 0x20),
            ["VerdictGreenBg"]    = Color.FromRgb(0xE0, 0xF5, 0xEA),
            ["VerdictGreenBd"]    = Color.FromRgb(0x60, 0xC0, 0x90),
            ["VerdictYellowBg"]   = Color.FromRgb(0xFD, 0xF5, 0xE0),
            ["VerdictYellowBd"]   = Color.FromRgb(0xD0, 0xA0, 0x40),
            ["VerdictRedBg"]      = Color.FromRgb(0xFD, 0xE8, 0xE8),
            ["VerdictRedBd"]      = Color.FromRgb(0xE0, 0x80, 0x80),
            ["TagAutoBg"]         = Color.FromRgb(0xE0, 0xF0, 0xFF),
            ["TagAutoFg"]         = Color.FromRgb(0x10, 0x60, 0xA0),
            ["TagAutoBd"]         = Color.FromRgb(0x80, 0xB0, 0xE0),
            ["TagSemiBg"]         = Color.FromRgb(0xFF, 0xF8, 0xE0),
            ["TagSemiFg"]         = Color.FromRgb(0x80, 0x60, 0x00),
            ["TagSemiBd"]         = Color.FromRgb(0xD0, 0xA0, 0x40),
            ["TagApiBg"]          = Color.FromRgb(0xF5, 0xE0, 0xFF),
            ["TagApiFg"]          = Color.FromRgb(0x70, 0x00, 0x90),
            ["TagApiBd"]          = Color.FromRgb(0xC0, 0x80, 0xE0),
        };

        private static readonly Dictionary<string, Color> DarkColors = new()
        {
            ["Bg0"]               = Color.FromRgb(0x0E, 0x0E, 0x15),
            ["Bg1"]               = Color.FromRgb(0x13, 0x13, 0x1E),
            ["Bg2"]               = Color.FromRgb(0x15, 0x15, 0x1F),
            ["Bg3"]               = Color.FromRgb(0x1A, 0x1A, 0x2C),
            ["Bd"]                = Color.FromRgb(0x2E, 0x30, 0x55),
            ["T1"]                = Color.FromRgb(0xDD, 0xE0, 0xF0),
            ["T2"]                = Color.FromRgb(0x80, 0x88, 0xC0),
            ["T3"]                = Color.FromRgb(0x55, 0x60, 0xA0),
            ["T4"]                = Color.FromRgb(0x3A, 0x3F, 0x70),
            ["ColGreen"]          = Color.FromRgb(0x3D, 0xDD, 0x80),
            ["ColYellow"]         = Color.FromRgb(0xF0, 0xB0, 0x30),
            ["ColRed"]            = Color.FromRgb(0xF0, 0x55, 0x55),
            ["VerdictGreenBg"]    = Color.FromRgb(0x09, 0x1A, 0x10),
            ["VerdictGreenBd"]    = Color.FromRgb(0x1A, 0x55, 0x35),
            ["VerdictYellowBg"]   = Color.FromRgb(0x25, 0x1C, 0x08),
            ["VerdictYellowBd"]   = Color.FromRgb(0x55, 0x3E, 0x10),
            ["VerdictRedBg"]      = Color.FromRgb(0x25, 0x0A, 0x0A),
            ["VerdictRedBd"]      = Color.FromRgb(0x55, 0x1A, 0x1A),
            ["TagAutoBg"]         = Color.FromRgb(0x0A, 0x1E, 0x28),
            ["TagAutoFg"]         = Color.FromRgb(0x30, 0x90, 0xC0),
            ["TagAutoBd"]         = Color.FromRgb(0x1A, 0x40, 0x60),
            ["TagSemiBg"]         = Color.FromRgb(0x1E, 0x18, 0x08),
            ["TagSemiFg"]         = Color.FromRgb(0xD0, 0xA0, 0x40),
            ["TagSemiBd"]         = Color.FromRgb(0x40, 0x30, 0x10),
            ["TagApiBg"]          = Color.FromRgb(0x1E, 0x0A, 0x1E),
            ["TagApiFg"]          = Color.FromRgb(0xC0, 0x80, 0xE0),
            ["TagApiBd"]          = Color.FromRgb(0x40, 0x18, 0x50),
        };

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new DashboardViewModel();
            // Default dark theme already applied via XAML resources.
        }

        // ─── Theme toggle ────────────────────────────────────────────
        private void BtnDark_Click(object sender, RoutedEventArgs e)
        {
            BtnDark.IsChecked  = true;
            BtnLight.IsChecked = false;
            ApplyTheme(DarkColors);
        }

        private void BtnLight_Click(object sender, RoutedEventArgs e)
        {
            BtnLight.IsChecked = true;
            BtnDark.IsChecked  = false;
            ApplyTheme(LightColors);
        }

        private void ApplyTheme(Dictionary<string, Color> palette)
        {
            foreach (var (key, color) in palette)
                Resources[key] = new SolidColorBrush(color);

            // Force background update on the window itself
            Background = (SolidColorBrush)Resources["Bg0"];
        }

        // ─── Layout toggle ───────────────────────────────────────────
        private void BtnLayout_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn) return;

            // Uncheck all layout buttons, check only the clicked one
            BtnCompact.IsChecked = false;
            BtnMedium.IsChecked  = false;
            BtnWide.IsChecked    = false;
            btn.IsChecked        = true;

            _columns = int.Parse(btn.Tag?.ToString() ?? "8");
            ApplyLayout(_columns);
        }

        private void ApplyLayout(int columns)
        {
            // Find the UniformGrid inside TilesControl's ItemsPanel
            var panel = FindUniformGrid(TilesControl);
            if (panel is not null)
                panel.Columns = columns;
        }

        // ─── Helper: find UniformGrid inside ItemsControl ────────────
        private static UniformGrid? FindUniformGrid(DependencyObject parent)
        {
            if (parent is null) return null;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is UniformGrid ug) return ug;
                var found = FindUniformGrid(child);
                if (found is not null) return found;
            }
            return null;
        }

        // ─── Show / hide detail panel ────────────────────────────────
        // Called by ViewModel when SelectedSection changes.
        // Wire this via an event or use a converter on the Visibility binding.
        public void ShowDetailPanel(bool visible)
        {
            DetailPanel.Visibility = visible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}

using System.Windows;
using DongNoti.Models;
using DongNoti.Services;

namespace DongNoti.Views
{
    public partial class FocusModeDialog : Window
    {
        public int SelectedMinutes { get; private set; }

        public FocusModeDialog()
        {
            InitializeComponent();
            LoadPresets();
        }

        private void LoadPresets()
        {
            var presets = FocusModeService.Instance.GetPresets();
            PresetListBox.ItemsSource = presets;

            // 기본 프리셋 선택
            var defaultPreset = FocusModeService.Instance.GetDefaultPreset();
            if (defaultPreset != null)
            {
                PresetListBox.SelectedItem = defaultPreset;
            }
            else if (presets.Count > 0)
            {
                PresetListBox.SelectedIndex = 0;
            }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (PresetListBox.SelectedItem is FocusModePreset preset)
            {
                SelectedMinutes = preset.Minutes;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("시간을 선택해주세요.", "집중 모드", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

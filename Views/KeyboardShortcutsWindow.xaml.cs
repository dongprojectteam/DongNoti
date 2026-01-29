using System.Collections.Generic;
using System.Windows;

namespace DongNoti.Views
{
    public partial class KeyboardShortcutsWindow : Window
    {
        public class ShortcutInfo
        {
            public string Description { get; set; } = string.Empty;
            public string Shortcut { get; set; } = string.Empty;
        }

        public KeyboardShortcutsWindow()
        {
            InitializeComponent();
            LoadShortcuts();
        }

        private void LoadShortcuts()
        {
            var shortcuts = new List<ShortcutInfo>
            {
                new ShortcutInfo { Description = "새 항목 추가", Shortcut = "Ctrl + N" },
                new ShortcutInfo { Description = "검색창 포커스", Shortcut = "Ctrl + F" },
                new ShortcutInfo { Description = "선택된 알람 삭제", Shortcut = "Delete" },
                new ShortcutInfo { Description = "선택된 알람 편집", Shortcut = "Enter / F2" },
                new ShortcutInfo { Description = "선택 해제", Shortcut = "Escape" }
            };

            ShortcutsItemsControl.ItemsSource = shortcuts;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

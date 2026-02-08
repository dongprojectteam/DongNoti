using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DongNoti.Services;

namespace DongNoti.Converters
{
    /// <summary>
    /// 카테고리명을 받아 설정된 카테고리 색상으로 Brush 또는 Thickness를 반환합니다.
    /// ConverterParameter: "Background" (연한 배경), "BorderBrush" (테두리 색), "BorderThickness" (좌측 강조 두께)
    /// </summary>
    public class CategoryColorConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var category = value as string;
            if (string.IsNullOrEmpty(category))
                return GetDefault(parameter);

            try
            {
                var settings = StorageService.LoadSettings();
                if (settings?.CategoryColors == null || !settings.CategoryColors.TryGetValue(category, out var hex) || string.IsNullOrEmpty(hex))
                    return GetDefault(parameter);

                var color = ParseHexColor(hex);
                if (!color.HasValue)
                    return GetDefault(parameter);

                var param = parameter?.ToString() ?? "";

                if (param == "Background" || param == "RowBackground")
                {
                    // 연한 배경: 색상에 흰색을 많이 섞음
                    var r = (byte)((color.Value.R + 255 * 15) / 16);
                    var g = (byte)((color.Value.G + 255 * 15) / 16);
                    var b = (byte)((color.Value.B + 255 * 15) / 16);
                    return new SolidColorBrush(Color.FromRgb(r, g, b));
                }
                if (param == "BorderBrush")
                    return new SolidColorBrush(color.Value);
                if (param == "BorderThickness")
                    return new Thickness(4, 0, 0, 1);
                if (param == "BadgeBackground")
                {
                    // 배지용 연한 배경
                    var r = (byte)((color.Value.R + 255 * 15) / 16);
                    var g = (byte)((color.Value.G + 255 * 15) / 16);
                    var b = (byte)((color.Value.B + 255 * 15) / 16);
                    return new SolidColorBrush(Color.FromRgb(r, g, b));
                }
                if (param == "BadgeBorderBrush")
                    return new SolidColorBrush(color.Value);
                if (param == "BadgeForeground")
                    return new SolidColorBrush(color.Value);
            }
            catch { }

            return GetDefault(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        private static object GetDefault(object? parameter)
        {
            var param = parameter?.ToString() ?? "";
            if (param == "Background")
                return new SolidColorBrush(Colors.White);
            if (param == "RowBackground")
                return new SolidColorBrush(Colors.Transparent);
            if (param == "BorderBrush")
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
            if (param == "BorderThickness")
                return new Thickness(0, 0, 0, 1);
            if (param == "BadgeBackground")
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F5F7"));
            if (param == "BadgeBorderBrush")
                return new SolidColorBrush(Colors.Transparent);
            if (param == "BadgeForeground")
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#546E7A"));
            return DependencyProperty.UnsetValue;
        }

        private static Color? ParseHexColor(string hex)
        {
            try
            {
                if (hex.StartsWith("#"))
                    hex = hex.Substring(1);
                if (hex.Length == 6)
                {
                    var r = System.Convert.ToByte(hex.Substring(0, 2), 16);
                    var g = System.Convert.ToByte(hex.Substring(2, 2), 16);
                    var b = System.Convert.ToByte(hex.Substring(4, 2), 16);
                    return Color.FromRgb(r, g, b);
                }
                if (hex.Length == 8)
                {
                    var a = System.Convert.ToByte(hex.Substring(0, 2), 16);
                    var r = System.Convert.ToByte(hex.Substring(2, 2), 16);
                    var g = System.Convert.ToByte(hex.Substring(4, 2), 16);
                    var b = System.Convert.ToByte(hex.Substring(6, 2), 16);
                    return Color.FromArgb(a, r, g, b);
                }
            }
            catch { }
            return null;
        }
    }
}

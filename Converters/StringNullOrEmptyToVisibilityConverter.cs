using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace InventarioApp.Converters
{
    public class StringNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            var invert = (parameter as string)?.ToLowerInvariant() == "invert";

            bool visible = !string.IsNullOrEmpty(str);
            if (invert) visible = !visible;

            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using Windows.UI.Xaml.Data;

namespace MyBlackBox.Converters
{
    class TimeSpanToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string result = "(" + ((TimeSpan) value).ToString(@"hh\:mm") + ")";
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return true;
        }
    }
}

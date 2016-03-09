using System;
using Windows.UI.Xaml.Data;

namespace MyBlackBox.Converters
{
    class DateTimeToDateTimeOffsetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            DateTime dateTime = ((DateTimeOffset)value).DateTime;
            return string.Format("{0:u}", dateTime);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return true;
        }
    }
}

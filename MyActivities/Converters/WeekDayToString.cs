using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace MyBlackBox.Converters
{
    class WeekDayToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string dateformat = string.Empty;

            //return (DateTime.Now.Date.AddDays((double)value)).ToString("dd/MMM/yyyy") + dateformat;
            var thedate = DateTime.Now.Date.AddDays((double) value);

            return thedate.ToString("dd/MMM/yyyy") + ": " + thedate.DayOfWeek + " vs Average " + DateTime.Now.DayOfWeek.ToString() + "s";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            string result = "";
            return result;
        }
    }
}

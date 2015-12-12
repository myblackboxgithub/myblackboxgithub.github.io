using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace MyActivities.Converters
{
    class TimeWindowToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string dateformat = string.Empty;
            if ((double)value == 0)
            {
                //twString = this._resourceLoader.GetString("TimeWindow/Today");
                dateformat = " - Today";
            }
            else if ((double)value == -1)
            {
                //twString = this._resourceLoader.GetString("TimeWindow/Yesterday");
                dateformat = " - Yesterday";
            }
            
            return (DateTime.Now.Date.AddDays((double)value)).ToString("dd/MMM/yyyy") + dateformat;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            string result = "";
            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WebApplication1
{
    public partial class WebForm1 : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Main_Process();

            
        }

        private void Main_Process()
        {
            ActivityReader activityReader = new ActivityReader();


            DateTime startDate = new DateTime(2015, 11, 23);
            DateTime endDate = startDate + TimeSpan.FromDays(1);

            IList<ActivityMonitorReading> historyData = ReadRAWDATA();

            Dictionary<Activity, TimeSpan> activitySummary = new Dictionary<Activity, TimeSpan>();
            var activityTypes = Enum.GetValues(typeof(Activity));
            foreach (var type in activityTypes)
            {
                activitySummary[(Activity)type] = TimeSpan.Zero;
            }

            Dictionary<TimeSpan, Dictionary<Activity, TimeSpan>> activitySummaryHours =
                new Dictionary<TimeSpan, Dictionary<Activity, TimeSpan>>();
            for (int i = 1; i <= 24; i++)
            {
                activitySummaryHours.Add(TimeSpan.FromHours(i), new Dictionary<Activity, TimeSpan>(activitySummary));
            }

            if (historyData.Count > 0)
            {
                int hour = 1;
                Activity currentActivity = historyData[0].Mode;
                DateTime currentDate = historyData[0].Timestamp.DateTime;

                for (int index = 1; index < historyData.Count; index++)
                {
                    var item = historyData[index];

                    TimeSpan duration = TimeSpan.Zero;
                    var checkHour = startDate.Date + TimeSpan.FromHours(hour);

                    if (item.Timestamp < checkHour)
                    {
                        duration = item.Timestamp - currentDate;
                        activitySummaryHours[TimeSpan.FromHours(hour)][currentActivity] += duration;

                        currentActivity = item.Mode;
                        currentDate = item.Timestamp.DateTime;
                    }
                    else
                    {
                        var indexOfHour = historyData.IndexOf(item);
                        var previousEvent = historyData[indexOfHour - 1];
                        var addedHour = new ActivityMonitorReading()
                        {
                            Mode = previousEvent.Mode,
                            Timestamp = startDate.Date + TimeSpan.FromHours(hour)
                        };
                        historyData.Insert(indexOfHour, addedHour);
                        duration = addedHour.Timestamp - previousEvent.Timestamp;
                        activitySummaryHours[TimeSpan.FromHours(hour)][previousEvent.Mode] += duration;

                        currentActivity = addedHour.Mode;
                        currentDate = addedHour.Timestamp.DateTime;

                        hour++;
                        if (hour == 25) break;
                    }
                }
            }

            for (int i = 1; i <= 24; i++)
            {
                foreach (var activityType in activityTypes)
                {
                    activityReader.ListData.Add(new ActivityDuration(i, (Activity)activityType, activitySummaryHours[TimeSpan.FromHours(i)][(Activity)activityType]));

                }
            }

            ListView1.DataSource = activityReader.ListData;

        }

        public class ActivityReader
        {
            private List<ActivityDuration> _listData = null;

            public List<ActivityDuration> ListData
            {
                get { return _listData; }
            }

            public ActivityReader()
            {
                _listData = new List<ActivityDuration>();
            }
        }

        public class ActivityDuration
        {
            public int Hour { get; set; }
            public Activity ActivityName { get; set; }
            public TimeSpan ActivityTimes { get; set; }

            public ActivityDuration(int hour, Activity activity, TimeSpan timeSpan)
            {
                Hour = hour;
                ActivityName = activity;
                ActivityTimes = timeSpan;
            }
        }

        private static string rawdata = "data.csv";
        private static string myDirectory = new DirectoryInfo(Environment.CurrentDirectory).Parent.Parent.FullName;
        private static string PATH = Path.Combine(myDirectory, rawdata);

        private static IList<ActivityMonitorReading> ReadRAWDATA()
        {
            string[] csvRows = File.ReadAllLines(PATH);
            IList<ActivityMonitorReading> readings = new List<ActivityMonitorReading>();

            string[] fields = null;
            foreach (string csvRow in csvRows)
            {
                fields = csvRow.Split(',');
                readings.Add(new ActivityMonitorReading()
                {
                    Mode = (Activity)int.Parse(fields[0]),
                    Timestamp = DateTimeOffset.Parse(fields[1])
                });
            }
            return readings;
        }
    }

    public enum Activity
    {
        Unknown = 0,
        Idle = 2,
        Moving = 4,
        Stationary = 8,
        Walking = 32,
        Running = 512,
        Biking = 1024,
        MovingInVehicle = 2048
    }

    public class ActivityMonitorReading
    {
        public Activity Mode { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }

}
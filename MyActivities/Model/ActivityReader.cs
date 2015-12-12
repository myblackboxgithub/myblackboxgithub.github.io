using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Lumia.Sense;

namespace MyActivities.Model
{
    public class ActivityReader : INotifyPropertyChanged
    {
        #region Private members
        /// <summary>
        /// List of activities and durations
        /// </summary>
        private List<ActivityDuration> _listData = null;

        /// <summary>
        /// Data instance
        /// </summary>
        private static ActivityReader _activityReader;

        /// <summary>
        /// List of history data
        /// </summary
        private IList<ActivityMonitorReading> _historyData;

        /// <summary>
        /// Activity monitor instance
        /// </summary>
        private IActivityMonitor _activityMonitor = null;

        /// <summary>
        /// Activity instance
        /// </summary>
        private Activity _activityMode = Activity.Idle;

        /// <summary>
        /// Time window index, 0 = today, -1 = yesterday 
        /// </summary>
        private double _timeWindowIndex = 0;
        #endregion

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// This method is called by the Set accessor of each property. 
        /// The CallerMemberName attribute that is applied to the optional propertyName 
        /// parameter causes the property name of the caller to be substituted as an argument.
        /// </summary>
        /// <param name="propertyName"></param>
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <summary>
        /// Constructor  
        /// </summary>
        public ActivityReader()
        {
            _listData = new List<ActivityDuration>();
        }

        /// <summary>
        /// Activity monitor property. Gets and sets the activity monitor
        /// </summary>
        public IActivityMonitor ActivityMonitorProperty
        {
            get
            {
                return _activityMonitor;
            }
            set
            {
                _activityMonitor = value;
            }
        }

        /// <summary>
        /// Create new instance of the class
        /// </summary>
        /// <returns>Data instance</returns>
        static public ActivityReader Instance()
        {
            if (_activityReader == null)
                _activityReader = new ActivityReader();
            return _activityReader;
        }

        /// <summary>
        /// Called when activity changes
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="args">Event arguments</param>
        public async void activityMonitor_ReadingChanged(IActivityMonitor sender, ActivityMonitorReading args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                this.ActivityEnum = args.Mode;
            });
        }

        /// <summary>
        /// Get the current activity
        /// </summary>
        public string CurrentActivity
        {
            get
            {
                return _activityMode.ToString().ToLower();
            }
        }

        /// <summary>
        /// Set the current activity
        /// </summary>
        public Activity ActivityEnum
        {
            set
            {
                _activityMode = value;
                NotifyPropertyChanged("CurrentActivity");
            }
        }

        /// <summary>
        /// Get the time window
        /// </summary>
        public double TimeWindow
        {
            get
            {
                return _timeWindowIndex;
            }
        }

        /// <summary>
        /// Set the time window to today
        /// </summary>
        public void NextDay()
        {
            if (_timeWindowIndex < 0)
            {
                _timeWindowIndex++;
                NotifyPropertyChanged("TimeWindow");
            }
        }

        /// <summary>
        /// Set the time window to previous day
        /// </summary>
        public void PreviousDay()
        {
            if (_timeWindowIndex >= -29)
            {
                _timeWindowIndex--;
                NotifyPropertyChanged("TimeWindow");
            }
        }

        /// <summary>
        /// List of activities occured during given time period.
        /// </summary>
        public IList<ActivityMonitorReading> History
        {
            get
            {
                return _historyData;
            }
            set
            {
                if (_historyData == null)
                {
                    _historyData = new List<ActivityMonitorReading>();
                }
                else
                {
                    _historyData.Clear();
                }
                _historyData = value;
                QuantifyData();
            }
        }

        //public string GetTheDate
        //{
        //    get
        //    {
        //        if(_historyData.Count != 0)
        //            return _historyData[0].Timestamp.DayOfWeek + " " +
        //                _historyData[0].Timestamp.Date.ToString("dd/MMM/yyyy");
        //        return string.Empty;
        //    }
        //}

        //public string GetNumberOfActivities
        //{
        //    get
        //    {
        //        if (_historyData.Count != 0)
        //            return _historyData.Count + "Times";
        //        return string.Empty;
        //    }
        //}


        //public string GetNumberOfActivities { get { return _historyData.Count + "Times"; } }
        //public string GetTheDate()
        //{
        //    return _historyData[0].Timestamp.DayOfWeek + " " + _historyData[0].Timestamp.Date.ToString("dd/MMM/yyyy");
        //}

        //public string GetNumberOfActivities()
        //{
        //    return _historyData.Count + "Times";
        //}



        /// <summary>
        /// Get the list of activities and durations 
        /// </summary>
        public List<ActivityDuration> ListData
        {
            get { return _listData; }
        }

        /// <summary>
        /// Populate the list of activities and durations to display in the UI 
        /// </summary>
        private void QuantifyData()
        {
            if (_listData != null)
            {
                _listData.Clear();
            }
            _listData = new List<ActivityDuration>();
            if (!Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                // Fetch activity history for the day
                DateTime startDate = DateTime.Today.Add(TimeSpan.FromDays(_timeWindowIndex));
                DateTime endDate = startDate + TimeSpan.FromDays(1);

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

                if (_historyData.Count > 0)
                {
                    bool hasDataInTimeWindow = false;
                    // Insert new fist entry, representing the last activity of the previous time window
                    // this helps capture that activity's duration but only from the start of current time window                    
                    ActivityMonitorReading first = _historyData[0];
                    if (first.Timestamp <= DateTime.Now.Date.AddDays(_timeWindowIndex))
                    {
                        // Create new "first" entry, with the same mode but timestamp set as 0:00h in current time window
                        _historyData.Insert(1, new ActivityMonitorReading(first.Mode, DateTime.Now.Date.AddDays(_timeWindowIndex)));
                        // Remove previous entry
                        _historyData.RemoveAt(0);
                        hasDataInTimeWindow = _historyData.Count > 1;
                    }
                    else
                    {
                        // The first entry belongs to the current time window
                        // there is no known activity before it
                        hasDataInTimeWindow = true;
                    }

                    if (hasDataInTimeWindow)
                    {
                        // Insert a last activity, marking the begining of the next time window
                        // this helps capturing the correct duration of the last activity stated in this time window
                        ActivityMonitorReading last = _historyData.Last();
                        if (last.Timestamp < DateTime.Now.Date.AddDays(_timeWindowIndex + 1))
                        {
                            // Is this today's time window
                            if (_timeWindowIndex == 0)
                            {
                                // Last activity duration measured until this instant time
                                _historyData.Add(new ActivityMonitorReading(last.Mode, DateTime.Now));
                            }
                            else
                            {
                                // Last activity measured until the begining of the next time index
                                _historyData.Add(new ActivityMonitorReading(last.Mode,
                                    DateTime.Now.Date.AddDays(_timeWindowIndex + 1)));
                            }
                        }

                        //divided the activities by every hour
                        //the code is buggy
                        int hour = 1;
                        Activity currentActivity = _historyData[0].Mode;
                        DateTime currentDate = _historyData[0].Timestamp.DateTime;

                        for (int index = 1; index < _historyData.Count - 1; index++)
                        {
                            var item = _historyData[index];

                            TimeSpan duration = TimeSpan.Zero;
                            var checkHour = startDate.Date + TimeSpan.FromHours(hour);

                            if (item.Timestamp < checkHour)
                            {
                                duration = item.Timestamp - currentDate;
                                activitySummaryHours[TimeSpan.FromHours(hour)][currentActivity] += duration;
                            }
                            else
                            {
                                var indexOfHour = _historyData.IndexOf(item);
                                var previousEvent = _historyData[indexOfHour - 1];
                                var addedHour = new ActivityMonitorReading()
                                {
                                    Mode = previousEvent.Mode,
                                    Timestamp = startDate.Date + TimeSpan.FromHours(hour)
                                };
                                _historyData.Insert(indexOfHour, addedHour);
                                duration = addedHour.Timestamp - previousEvent.Timestamp;
                                activitySummaryHours[TimeSpan.FromHours(hour)][previousEvent.Mode] += duration;

                                hour++;
                                if (hour == 25) break;
                            }
                            currentActivity = item.Mode;
                            currentDate = item.Timestamp.DateTime;
                        }
                    }
                }

                for (int hour = 1; hour <= 24; hour++)
                {
                    foreach (var activityType in activityTypes)
                    {
                        if ((Activity)activityType != Activity.Idle)
                            activitySummaryHours[TimeSpan.FromHours(hour)][Activity.Unknown] +=
                                activitySummaryHours[TimeSpan.FromHours(hour)][(Activity)activityType];
                    }
                }

                for (var hour = 1; hour <= 24; hour++)
                {
                    foreach (var activityType in activityTypes)
                    {
                        if ((Activity)activityType == Activity.Idle || (Activity)activityType == Activity.Unknown)
                            _listData.Add(new ActivityDuration(hour, (Activity)activityType, activitySummaryHours[TimeSpan.FromHours(hour)][(Activity)activityType]));
                    }
                }
            }
            NotifyPropertyChanged("ListData");
        }
    }

    /// <summary>
    ///  Helper class to create a list of activities and their timestamp 
    /// </summary>
    public class ActivityDuration
    {
        public int Hour { get; set; }



        public Activity ActivityName { get; set; }
        public TimeSpan ActivityTime { get; set; }

        public ActivityDuration(int h, Activity s, TimeSpan i)
        {
            Hour = h;


            //split activity string by capital letter
            //Type = System.Text.RegularExpressions.Regex.Replace(s, @"([A-Z])(?<=[a-z]\1|[A-Za-z]\1(?=[a-z]))", " $1");
            ActivityName = s;
            ActivityTime = i;
        }
    }





    public class MyActivity
    {
        public int Hour { get; set; }
        public TimeSpan Idle { get; set; }
        public TimeSpan Activities { get; set; }
    }

    public enum MyActivityEnum
    {
        Idle, Activity
    }
}

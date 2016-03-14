using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization.DateTimeFormatting;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.StartScreen;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ClassLibrary;
using Lumia.Sense;
using MyBlackBox;
using SQLite.Net;
using SQLite.Net.Async;
using SQLiteNetExtensionsAsync.Extensions;
using SQLite.Net.Platform.WinRT;
using WinRTXamlToolkit.Controls.DataVisualization.Charting;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace MyActivities
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SensorsDB : Page
    {
        private ActivityMonitor _aMonitor;
        private const string tileId = "SecondTile.SensorDB";
        private static string SQLiteFile = "MyBlackBox.sqlite3";
        private static string DBPATH = Path.Combine(ApplicationData.Current.LocalFolder.Path, SQLiteFile);

        public SensorsDB()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;
            this.Loaded += SensorsDB_Loaded;

            this.DataContext = ActivityReader.Instance();
        }

        //it is to covert to timespan to int
        class MyDuration
        {
            public int Hour { get; set; }
            public Activity ActivityName { get; set; }
            public int ActivityTime { get; set; }
        }

        private async void SensorsDB_Loaded(object sender, RoutedEventArgs e)
        {
            if (!await CallSensorcoreApiAsync(async () =>
            {
                if (_aMonitor == null)
                {
                    _aMonitor = await ActivityMonitor.GetDefaultAsync();
                }
                else
                {
                    await _aMonitor.ActivateAsync();
                }

                ActivityReader.Instance().History =
                    await _aMonitor.GetActivityHistoryAsync(DateTime.Now.Date.AddDays(ActivityReader.Instance().TimeWindow), new TimeSpan(24, 0, 0));

            }))
            {
                if (_aMonitor != null) await CallSensorcoreApiAsync(async () => await _aMonitor.DeactivateAsync());
            }

            DisplayLineChart1();
            await DisplayLineChart2();
            DisplayPieChart();
        }

        private async void PollHistory(double timeWindow)
        {
            if (await CallSensorcoreApiAsync(async () =>
            {
                if (_aMonitor != null)
                {
                    ActivityReader.Instance().History =
                        await _aMonitor.GetActivityHistoryAsync(DateTime.Now.Date.AddDays(timeWindow), new TimeSpan(24, 0, 0));
                }
            }))
            {
                //if (_aMonitor != null) await CallSensorcoreApiAsync(async () => await _aMonitor.DeactivateAsync());
            }

            DisplayLineChart1();
            await DisplayLineChart2();
            DisplayPieChart();
        }

        private async Task<bool> CallSensorcoreApiAsync(Func<Task> action)
        {
            Exception failure = null;
            try
            {
                await action();
            }
            catch (Exception e)
            {
                failure = e;
            }
            if (failure != null)
            {
                MessageDialog dialog;
                switch (SenseHelper.GetSenseError(failure.HResult))
                {
                    case SenseError.LocationDisabled:
                        dialog = new MessageDialog("Location has been disabled. Do you want to open Location settings now?", "Information");
                        dialog.Commands.Add(new UICommand("Yes", async cmd => await SenseHelper.LaunchLocationSettingsAsync()));
                        dialog.Commands.Add(new UICommand("No"));
                        await dialog.ShowAsync();
                        new System.Threading.ManualResetEvent(false).WaitOne(500);
                        return false;
                    case SenseError.SenseDisabled:
                        dialog = new MessageDialog("Motion data has been disabled. Do you want to open Motion data settings now?", "Information");
                        dialog.Commands.Add(new UICommand("Yes", async cmd => await SenseHelper.LaunchSenseSettingsAsync()));
                        dialog.Commands.Add(new UICommand("No"));
                        await dialog.ShowAsync();
                        new System.Threading.ManualResetEvent(false).WaitOne(500);
                        return false;
                    case SenseError.SensorNotAvailable:
                        dialog = new MessageDialog("The sensor is not supported on this device", "Information");
                        await dialog.ShowAsync();
                        new System.Threading.ManualResetEvent(false).WaitOne(500);
                        return false;
                    default:
                        dialog = new MessageDialog("Failure: " + SenseHelper.GetSenseError(failure.HResult), "");
                        await dialog.ShowAsync();
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await RegisterBackgoundTask();

            DisplayInformation.AutoRotationPreferences = DisplayOrientations.LandscapeFlipped;
        }

        private async Task RegisterBackgoundTask()
        {
            try
            {
                BackgroundAccessStatus status = await BackgroundExecutionManager.RequestAccessAsync();
                if (status != BackgroundAccessStatus.Denied)
                {
                    bool isCheckingTaskRegistered =
                    BackgroundTaskRegistration.AllTasks.Any(x => x.Value.Name == "CheckingTask");

                    //to make trigger at 0clock to compare the data
                    var nextOClock = new TimeSpan(DateTime.Now.AddHours(1).Hour, 0, 0);
                    var timeTilNextOClock = nextOClock - DateTime.Now.TimeOfDay;
                    var minutesTillNextHour = (uint)timeTilNextOClock.TotalMinutes;

                    if (!isCheckingTaskRegistered)
                    {
                        BackgroundTaskBuilder checkingTaskBuilder = new BackgroundTaskBuilder()
                        {
                            Name = "CheckingTask",
                            TaskEntryPoint = "ActivityBackground.CheckingTask"
                        };
                        checkingTaskBuilder.SetTrigger(new TimeTrigger(minutesTillNextHour, false));
                        checkingTaskBuilder.Register();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("The acess has already been granted: " + e.Message);
            }
        }

        private void Details_OnClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ViewDetails), ActivityReader.Instance().History);
        }

        private void Previous_OnClick(object sender, RoutedEventArgs e)
        {
            ActivityReader.Instance().PreviousDay();
            btnNext.IsEnabled = true;
            btnPrevious.IsEnabled = ActivityReader.Instance().TimeWindow > -30;
            PollHistory(ActivityReader.Instance().TimeWindow);
        }

        private void Next_OnClick(object sender, RoutedEventArgs e)
        {
            ActivityReader.Instance().NextDay();
            btnNext.IsEnabled = ActivityReader.Instance().TimeWindow < 0;
            btnPrevious.IsEnabled = true;
            PollHistory(ActivityReader.Instance().TimeWindow);
        }

        private void BtnUI_OnClick(object sender, RoutedEventArgs e)
        {

        }

        private async void BtnPin_OnClick(object sender, RoutedEventArgs e)
        {
            bool isExistTile = SecondaryTile.Exists(tileId);
            if (!isExistTile)
            {
                //uint stepCount = await App.Engine.GetStepCountAsync();
                //uint meter = Helper.GetMeter(stepCount);
                //uint meterSmall = Helper.GetSmallMeter(stepCount);

                //uint stepCount = 27946;
                uint level = 1;
                uint levelSmall = 1;

                try
                {

                    var secondaryTile = new SecondaryTile(
                            tileId,
                            "My BlackBox",
                            "/SensorsDB.xaml",
                             new Uri("ms-appx:///Assets/level" + levelSmall + ".png", UriKind.Absolute),
                            TileSize.Square150x150);

                    secondaryTile.VisualElements.Square71x71Logo = new Uri("ms-appx:///Assets/level" + levelSmall + ".png", UriKind.Absolute);
                    secondaryTile.VisualElements.ShowNameOnSquare150x150Logo = true;
                    //secondaryTile.VisualElements.ShowNameOnSquare310x310Logo = false;
                    secondaryTile.VisualElements.ShowNameOnWide310x150Logo = false;
                    secondaryTile.VisualElements.BackgroundColor = Colors.SkyBlue;

                    //secondaryTile.VisualElements.Wide310x150Logo = new Uri("ms-appx:///Assets/wide" + meter + ".png", UriKind.Absolute);
                    //secondaryTile.RoamingEnabled = false;

                    await secondaryTile.RequestCreateAsync();

                }
                catch (Exception exp)
                {

                }
                return;
            }
            else
            {

                SecondaryTile secondaryTile = new SecondaryTile(tileId);
                await secondaryTile.RequestDeleteAsync();

                if (!SecondaryTile.Exists(tileId))
                {
                    btnPin.Icon = new SymbolIcon(Symbol.Pin);
                    btnPin.Label = "Pin";
                }
                else
                {
                    btnPin.Icon = new SymbolIcon(Symbol.UnPin);
                    btnPin.Label = "UnPin";
                }

            }

        }

        public static List<T> FindChildrenByControl<T>(DependencyObject root) where T : FrameworkElement
        {
            var list = new List<T>();

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is T)
                {
                    list.Add(child as T);
                }
                else
                {
                    list.AddRange(FindChildrenByControl<T>(child));
                }
            }
            return list;
        }

        private void DisplayLineChart1()
        {
            List<MyDuration> myActive = new List<MyDuration>();
            List<MyDuration> myIdle = new List<MyDuration>();
            foreach (var linedata in ActivityReader.Instance().ListData)
            {
                if (DateTime.Now.Hour < linedata.Hour - 1 && ActivityReader.Instance().TimeWindow == 0)
                    break;

                if (linedata.ActivityName == Activity.Idle)
                    myIdle.Add(new MyDuration() { Hour = linedata.Hour, ActivityTime = (int)linedata.ActivityTime.TotalMinutes });
                else if ((linedata.ActivityName == Activity.Unknown))
                    myActive.Add(new MyDuration() { Hour = linedata.Hour, ActivityTime = (int)linedata.ActivityTime.TotalMinutes });
            }

            var chart1Test = FindChildrenByControl<Chart>(Hub).FirstOrDefault(x => x.Name == "LineChart1");

            ((LineSeries)chart1Test.Series[0]).ItemsSource = myActive;
            ((LineSeries)chart1Test.Series[1]).ItemsSource = myIdle;
        }

        private void DisplayPieChart()
        {
            int totalIdle = 0, totalMoving = 0, totalStationary = 0,
                totalWalking = 0, totalRunning = 0, totalBiking = 0, totalMovinginVehicle = 0;

            foreach (var instance in ActivityReader.Instance().ListData)
            {
                switch (instance.ActivityName)
                {
                    case Activity.Idle:
                        totalIdle += (int)instance.ActivityTime.TotalMilliseconds;
                        break;
                    case Activity.Moving:
                        totalMoving += (int)instance.ActivityTime.TotalMilliseconds;
                        break;
                    case Activity.Stationary:
                        totalStationary += (int)instance.ActivityTime.TotalMilliseconds;
                        break;
                    case Activity.Walking:
                        totalWalking += (int)instance.ActivityTime.TotalMilliseconds;
                        break;
                    case Activity.Running:
                        totalRunning += (int)instance.ActivityTime.TotalMilliseconds;
                        break;
                    case Activity.Biking:
                        totalBiking += (int)instance.ActivityTime.TotalMilliseconds;
                        break;
                    case Activity.MovingInVehicle:
                        totalMovinginVehicle += (int)instance.ActivityTime.TotalMilliseconds;
                        break;
                }
            }
            List<MyDuration> myActive = new List<MyDuration>()
            {
                new MyDuration() { ActivityName = Activity.Idle, ActivityTime = totalIdle},
                new MyDuration() { ActivityName = Activity.Moving, ActivityTime = totalMoving},
                new MyDuration() { ActivityName = Activity.Stationary, ActivityTime = totalStationary},
                new MyDuration() { ActivityName = Activity.Walking, ActivityTime = totalWalking},
                new MyDuration() { ActivityName = Activity.Running, ActivityTime = totalRunning},
                new MyDuration() { ActivityName = Activity.Biking, ActivityTime = totalBiking},
                new MyDuration() { ActivityName = Activity.MovingInVehicle, ActivityTime = totalMovinginVehicle}
            };

            var pieChart = FindChildrenByControl<Chart>(Hub).FirstOrDefault(x => x.Name == "PieChart");
            ((PieSeries)pieChart.Series[0]).ItemsSource = myActive;
        }

        private async Task DisplayLineChart2()
        {
            List<MyDuration> currentActive = new List<MyDuration>();

            foreach (var linedata in ActivityReader.Instance().ListData)
            {
                if (DateTime.Now.Hour < linedata.Hour - 1)
                    break;

                if (linedata.ActivityName == Activity.Unknown)
                    currentActive.Add(new MyDuration() { Hour = linedata.Hour, ActivityTime = (int)linedata.ActivityTime.TotalMinutes });
            }

            var chart2Test = FindChildrenByControl<Chart>(Hub).FirstOrDefault(x => x.Name == "LineChart2");

            ((LineSeries)chart2Test.Series[0]).ItemsSource = currentActive;


            //To replace sunday value = 7
            var myWeekday = DateTime.Now.DayOfWeek;
            if (myWeekday == (int)DayOfWeek.Sunday)
            {
                myWeekday = (DayOfWeek)myDayWeek.sunday;
            }

            using (var connection = new SQLiteConnectionWithLock(new SQLitePlatformWinRT(), new SQLiteConnectionString(DBPATH, false)))
            {
                var dbConn = new SQLiteAsyncConnection(() => connection);

                WeekHistory dayofWeek = await dbConn.GetWithChildrenAsync<WeekHistory>(myWeekday, true);

                List<MyDuration> pastAvgActive = new List<MyDuration>();
                for (int i = 0; i < 48; i++)
                {
                    if (i % 2 == 0)
                        pastAvgActive.Add(new MyDuration() { Hour = dayofWeek.DayHistorys[i].Hour, ActivityTime = (int)dayofWeek.DayHistorys[i].ActivityTime.TotalMinutes });
                }

                ((LineSeries)chart2Test.Series[1]).ItemsSource = pastAvgActive;

            }
        }

        private void btnSetting_OnClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SensorSetting));
        }
    }
}

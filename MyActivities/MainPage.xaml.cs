using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Lumia.Sense;
using MyActivities.Model;
using SQLite;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace MyActivities
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private ActivityMonitor _aMonitor;

        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            this.Loaded += MainPage_Loaded;
            this.DataContext = ActivityReader.Instance();
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
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
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            // TODO: Prepare page for display here.

            // TODO: If your application contains multiple pages, ensure that you are
            // handling the hardware Back button by registering for the
            // Windows.Phone.UI.Input.HardwareButtons.BackPressed event.
            // If you are using the NavigationHelper provided by some templates,
            // this event is handled for you.







            await Register_BackgroundTask();
        }


        private async void RegisterBtn_Click(object sender, RoutedEventArgs e)
        {
            string myTaskName = "TimeZoneTask";

            // check if task is already registered
            foreach (var cur in BackgroundTaskRegistration.AllTasks)
                if (cur.Value.Name == myTaskName)
                {
                    await (new MessageDialog("Task already registered")).ShowAsync();
                    return;
                }

            // Windows Phone app must call this to use trigger types (see MSDN)
            await BackgroundExecutionManager.RequestAccessAsync();

            // register a new task
            BackgroundTaskBuilder taskBuilder = new BackgroundTaskBuilder { Name = "TimeZoneTask", TaskEntryPoint = "BackgroundTask.TimeZoneTask" };
            taskBuilder.SetTrigger(new TimeTrigger(15, true));
            BackgroundTaskRegistration myFirstTask = taskBuilder.Register();

            await (new MessageDialog("Task registered")).ShowAsync();
        }


        async Task Register_BackgroundTask()
        {
            string myTaskName = "twohoursTask";

            // check if task is already registered
            foreach (var cur in BackgroundTaskRegistration.AllTasks)
                if (cur.Value.Name == myTaskName)
                {
                    await (new MessageDialog("Task already registered")).ShowAsync();
                    return;
                }

            await BackgroundExecutionManager.RequestAccessAsync();
            var builder = new BackgroundTaskBuilder();
            builder.Name = myTaskName;
            //var condition = new SystemCondition(SystemTriggerType.TimeZoneChange, false);
            //var trigger = new SystemTrigger(SystemTriggerType.TimeZoneChange, false);
            var trigger = new TimeTrigger(60, true);
            builder.TaskEntryPoint = typeof(ActivityBackground.BackTask).FullName;
            //builder.AddCondition(condition);
            builder.SetTrigger(trigger);
            builder.Register();



            //await (new MessageDialog("My 2 hour background task added")).ShowAsync();
        }





        private void BtnSave_OnClick(object sender, RoutedEventArgs e)
        {
            List<SQLActivity> sqlActivities = new List<SQLActivity>();
            foreach (var activity in ActivityReader.Instance().History)
            {
                sqlActivities.Add(new SQLActivity()
                {
                    Mode = activity.Mode,
                    DateTime = activity.Timestamp.DateTime
                });
            }

            using (var dbConn = new SQLiteConnection(App.DBPATH))
            {
                dbConn.InsertAll(sqlActivities);
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
            btnPrevious.IsEnabled = ActivityReader.Instance().TimeWindow > -10;
            PollHistory(ActivityReader.Instance().TimeWindow);
        }

        private void Next_OnClick(object sender, RoutedEventArgs e)
        {
            ActivityReader.Instance().NextDay();
            btnNext.IsEnabled = ActivityReader.Instance().TimeWindow < 0;
            btnPrevious.IsEnabled = true;
            PollHistory(ActivityReader.Instance().TimeWindow);
        }
    }
}

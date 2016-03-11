using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.Devices.Geolocation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls.Maps;
using ActivityBackground.DataModel;
using ClassLibrary;
using Lumia.Sense;
using Microsoft.WindowsAzure.MobileServices;
using SQLite.Net;
using SQLite.Net.Async;
using SQLite.Net.Platform.WinRT;
using SQLiteNetExtensions.Extensions;
using SQLiteNetExtensionsAsync.Extensions;

namespace ActivityBackground
{
    public sealed class CheckingTask : IBackgroundTask
    {
        private static string SQLiteFile = "MyBlackBox.sqlite3";
        private static string DBPATH = Path.Combine(ApplicationData.Current.LocalFolder.Path, SQLiteFile);
        private ActivityMonitor _aMonitor;
        private const string tileId = "SecondTile.SensorDB";

        private static MobileServiceClient MobileService 
            = new MobileServiceClient("https://blackbox-js.azure-mobile.net/","uYTKaYiQCJTsIoGqTdMeLxgbntBFxA56");

        //private MobileServiceCollection<User, User> users;
        private MobileServiceCollection<ToastContent, ToastContent> toasts;
        //private IMobileServiceTable<User> userTable = MobileService.GetTable<User>();
        private IMobileServiceTable<ToastContent> toastTable = MobileService.GetTable<ToastContent>();

        private string patientStatus = string.Empty;
        private double latitude, longitude;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral deferral = taskInstance.GetDeferral();

            await SetInitalizeSensors();

            await CheckStatusEveryHour();

            await SaveDailyToDb();

            await SendNotificationToAzure(DateTime.Now.ToString(@"h\:mm tt"));

            deferral.Complete();
        }

        private async Task SaveDailyToDb()
        {
            var tonightZeroOclock = DateTime.Today.AddDays(1);
            var tonight10MinBefore = tonightZeroOclock.AddMinutes(-15);
            var tonight10MinAfter = tonightZeroOclock.AddMinutes(15);
            if (DateTime.Now > tonight10MinBefore && DateTime.Now < tonight10MinAfter)
            {
                //Debug.WriteLine("it is time to save DB");
                await SaveTodayDb();
            }

            //Debug.WriteLine("SaveDailyToDb called");
            //Debug.WriteLine(tonight10MinBefore + ":" + DateTime.Now + ":" + tonight10MinAfter);
        }

        private async Task CheckStatusEveryHour()
        {
            //get current data from sensorcore
            await PollHistory(ActivityReader.Instance().TimeWindow);

            List<ActivityDuration> activityDurations = ActivityReader.Instance().ListData;

            //foreach (var action in activityDurations)
            //{
            //    if (action.ActivityName == Activity.Unknown || action.ActivityName == Activity.Idle)
            //        Debug.WriteLine(action.Hour + ":" + action.ActivityName + ":" + action.ActivityTime);
            //}

            var timeSlot = DateTime.Now.Hour;

            var currentActivieTime 
                = activityDurations
                .Where(x => x.Hour == timeSlot)
                .First(x => x.ActivityName == Activity.Unknown).ActivityTime;

            var currentIdleTime
                = activityDurations
                .Where(x => x.Hour == timeSlot)
                .First(x => x.ActivityName == Activity.Idle).ActivityTime;

             //get average data from SQLite
            TimeSpan avgIdleTime = TimeSpan.Zero;
            TimeSpan avgActivieTime = TimeSpan.Zero;

            //replace sunday value = 7
            var myWeekday = DateTime.Now.DayOfWeek;
            if (myWeekday == (int)DayOfWeek.Sunday)
            {
                myWeekday = (DayOfWeek)myDayWeek.sunday;
            }

            using (
                var connection = new SQLiteConnectionWithLock(new SQLitePlatformWinRT(),
                    new SQLiteConnectionString(DBPATH, false)))
            {
                var dbConn = new SQLiteAsyncConnection(() => connection);

                WeekHistory dayofWeek = await dbConn.GetWithChildrenAsync<WeekHistory>(myWeekday, true);

                avgIdleTime = dayofWeek.DayHistorys
                    .Where(x => x.Hour == timeSlot)
                    .First(a => a.ActivityName == Activity.Idle).ActivityTime;
                avgActivieTime = dayofWeek.DayHistorys
                    .Where(x => x.Hour == timeSlot)
                    .First(a => a.ActivityName == Activity.Unknown).ActivityTime;
            }

            //Debug.WriteLine("Current, Active: " + currentActivieTime + ", Idle: " + currentIdleTime);
            //Debug.WriteLine("Average, Active: " + avgActivieTime + ", Idle: " + avgIdleTime);


            //SendNotification((timeSlot-1) + "~" + timeSlot
            //                 + " Cur:" + (int)currentActivieTime.TotalMinutes + " Avg:" + (int)avgActivieTime.TotalMinutes);


            //get the slidervalue from the textfile
            SettingZone settingZone = await ReadSliderSetting();

            //Debug.WriteLine(settingZone.GreenLight + " : " + settingZone.Redlight);

            //read geolocation
            Geoposition geoposition = await GetGeoLocation();
            latitude = geoposition.Coordinate.Point.Position.Latitude;
            longitude = geoposition.Coordinate.Point.Position.Longitude;

            CalculateStatus(avgActivieTime, currentActivieTime, settingZone, timeSlot);
        }

        private void CalculateStatus(TimeSpan avgActivieTime, TimeSpan currentActivieTime, SettingZone settingZone,
            int timeSlot)
        {
            var difference = (int) (avgActivieTime - currentActivieTime).TotalMinutes;
            if (difference > settingZone.Redlight) //redzone
            {
                patientStatus = "Alert";
                SendNotification("Alert," + DateTime.Now.ToString(@"h\:mm") + ",Lat:" + latitude + ",Lon:" + longitude);
                UpdateTile(currentActivieTime, avgActivieTime, timeSlot, 3);
            }
            else if (difference > settingZone.GreenLight) //yellowzone
            {
                patientStatus = "Warning";
                SendNotification("Warning," + DateTime.Now.ToString(@"h\:mm") + ",Lat:" + latitude + ",Lon:" + longitude);
                UpdateTile(currentActivieTime, avgActivieTime, timeSlot, 2);
            }
            else
            {
                patientStatus = "Fine";
                SendNotification("Fine," + DateTime.Now.ToString(@"h\:mm") + ",Lat:" + latitude + ",Lon:" + longitude);
                UpdateTile(currentActivieTime, avgActivieTime, timeSlot, 1);
            }
        }

        private async Task SendNotificationToAzure(string alertTime)
        {
            var toast = new ToastContent { toasttext = patientStatus, alert_time = alertTime, Longitude = latitude, Latitude = longitude };
            // await InsertUserObject(user);
            try
            {
                await InsertToastObject(toast);
                await RefreshToastItems();
            }
            catch (Exception ex)
            {
                await new MessageDialog(ex.Message, "Error loading toasts").ShowAsync();
            }
        }


        private static async Task<SettingZone> ReadSliderSetting()
        {
            string filename = "setting.txt";

            StorageFolder storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            StorageFile file = await storageFolder.GetFileAsync(filename);

            string text = await Windows.Storage.FileIO.ReadTextAsync(file);

            SettingZone settingZone = new SettingZone()
            {
                GreenLight = int.Parse(text.Split(',')[0]),
                Redlight = int.Parse(text.Split(',')[1])
            };

            return settingZone;
        }

        private static async Task<Geoposition> GetGeoLocation()
        {
            Geolocator geolocator = new Geolocator
            {
                DesiredAccuracyInMeters = 50
            };

            Geoposition geoposition = await geolocator.GetGeopositionAsync(
                maximumAge: TimeSpan.FromMinutes(5),
                timeout: TimeSpan.FromSeconds(10));

            return geoposition;
        }


        private void UpdateTile(TimeSpan currentTime, TimeSpan avgTime, int timeSlot, int level)
        {
            string status;
            switch (level)
            {
                case 3:
                    status = "Alert";
                    break;
                case 2:
                    status = "Warning";
                    break;
                case 1:
                    status = "Fine";
                    break;
                default:
                    status = "Unknown";
                    break;
            }

            //Debug.WriteLine(level + ":" + status);

            var currentTimeToString = currentTime.ToString(@"h\:mm");
            var averageIdleToString = avgTime.ToString(@"h\:mm");

            //var fromTTSlot = timeSlot.AddHours(-1);
            //var toTTSlot = timeSlot;
            //var fromHour = fromTTSlot.ToString("tt", CultureInfo.InvariantCulture);
            //var toHour = toTTSlot.ToString("tt", CultureInfo.InvariantCulture);
            var fromTTSlot = timeSlot - 1;
            var toTTSlot = timeSlot;

            //Debug.WriteLine(status + ": " + currentIdle + " vs " + averageIdle + " -------------------------");

            var mediumTitle = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquare150x150PeekImageAndText01);
            var tileTextAttributes = mediumTitle.GetElementsByTagName("text");
            tileTextAttributes[0].AppendChild(mediumTitle.CreateTextNode(status));
            tileTextAttributes[1].AppendChild(mediumTitle.CreateTextNode(fromTTSlot + " ~ " + toTTSlot));
            tileTextAttributes[2].AppendChild(mediumTitle.CreateTextNode("Current: " + currentTimeToString + "min"));
            tileTextAttributes[3].AppendChild(mediumTitle.CreateTextNode("Average: " + averageIdleToString + "min"));

            var bindingTile = (XmlElement)mediumTitle.GetElementsByTagName("binding").Item(0);
            bindingTile.SetAttribute("branding", "none");

            XmlNodeList img = mediumTitle.GetElementsByTagName("image");
            ((XmlElement)img[0]).SetAttribute("src", "ms-appx:///Assets/level" + level + ".png");

            var tileNotification = new TileNotification(mediumTitle);
            var tileUpdater = TileUpdateManager.CreateTileUpdaterForSecondaryTile(tileId);

            tileUpdater.Update(tileNotification);
        }

        private async Task PollHistory(double timeWindow)
        {
            if (_aMonitor != null)
            {
                ActivityReader.Instance().History =
                    await _aMonitor.GetActivityHistoryAsync(DateTime.Now.Date.AddDays(timeWindow), new TimeSpan(24, 0, 0));
            }
        }

        private async Task SetInitalizeSensors()
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
            }))
            {
                if (_aMonitor != null) await CallSensorcoreApiAsync(async () => await _aMonitor.DeactivateAsync());
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

        private void SendNotification(string text)
        {
            XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText01);
            XmlNodeList elements = toastXml.GetElementsByTagName("text");
            foreach (var node in elements)
            {
                node.InnerText = text;
            }
            ToastNotification notification = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier().Show(notification);
        }

        private async Task SaveTodayDb()
        {
            await SetInitalizeDb();

            await SaveSQliteDb();
        }
        
        /// <summary>
        /// BackTask to save DB everyday
        /// </summary>
        /// <returns></returns>
        private async Task SetInitalizeDb()
        {
            if (!CheckFileExists(SQLiteFile).Result)
            {
                using (var connection = new SQLiteConnectionWithLock(new SQLitePlatformWinRT(), new SQLiteConnectionString(DBPATH, false)))
                {
                    var db = new SQLiteAsyncConnection(() => connection);

                    await db.CreateTableAsync<WeekHistory>();
                    await db.CreateTableAsync<DayHistory>();

                    List<WeekHistory> weekHistories = new List<WeekHistory>()
                    {
                        new WeekHistory() {myDayWeek = myDayWeek.monday, NoOfDays = 0},
                        new WeekHistory() {myDayWeek = myDayWeek.tuesday, NoOfDays = 0},
                        new WeekHistory() {myDayWeek = myDayWeek.wednesday, NoOfDays = 0},
                        new WeekHistory() {myDayWeek = myDayWeek.thursday, NoOfDays = 0},
                        new WeekHistory() {myDayWeek = myDayWeek.friday, NoOfDays = 0},
                        new WeekHistory() {myDayWeek = myDayWeek.saturday, NoOfDays = 0},
                        new WeekHistory() {myDayWeek = myDayWeek.sunday, NoOfDays = 0}
                    };
                    await db.InsertAllAsync(weekHistories);

                    for (int dayofWeek = 0; dayofWeek < 7; dayofWeek++)
                    {
                        List<DayHistory> dayHistories = new List<DayHistory>() { };
                        for (int i = 1; i <= 24; i++)
                        {
                            dayHistories.Add(new DayHistory()
                            {
                                Hour = i,
                                ActivityName = Activity.Unknown,
                                ActivityTime = new TimeSpan(0, 0, 0)
                            });
                            dayHistories.Add(new DayHistory()
                            {
                                Hour = i,
                                ActivityName = Activity.Idle,
                                ActivityTime = new TimeSpan(0, 0, 0)
                            });
                        }

                        await db.InsertAllAsync(dayHistories);
                        weekHistories[dayofWeek].DayHistorys = dayHistories;
                        await db.UpdateWithChildrenAsync(weekHistories[dayofWeek]);
                    }
                }
            }
        }

        private async Task SaveSQliteDb()
        {
            var saveDate = DateTime.Today.Add(TimeSpan.FromDays(ActivityReader.Instance().TimeWindow));


            SendNotification(saveDate.DayOfWeek + ", " + saveDate.ToString("dd/MMM/yyyy") + ", saved!");


            await PollHistory(ActivityReader.Instance().TimeWindow);

            List<ActivityDuration> activityDurations = ActivityReader.Instance().ListData;

            //to replace myweekday with sunday = 7
            var myWeekday = saveDate.DayOfWeek;
            if (myWeekday == (int)DayOfWeek.Sunday)
            {
                myWeekday = (DayOfWeek)myDayWeek.sunday;
            }

            using (var connection = new SQLiteConnectionWithLock(new SQLitePlatformWinRT(), new SQLiteConnectionString(DBPATH, false)))
            {
                var dbConn = new SQLiteAsyncConnection(() => connection);

                WeekHistory dayofWeek = await dbConn.GetWithChildrenAsync<WeekHistory>(myWeekday, true);

                dayofWeek.NoOfDays++;
                await dbConn.UpdateAsync(dayofWeek);

                TimeSpan span = TimeSpan.Zero;
                for (int i = 0; i < dayofWeek.DayHistorys.Count; i++)
                {
                    span = activityDurations[i].ActivityTime - dayofWeek.DayHistorys[i].ActivityTime;
                    dayofWeek.DayHistorys[i].ActivityTime += new TimeSpan(span.Ticks / dayofWeek.NoOfDays);
                }
                await dbConn.UpdateAllAsync(dayofWeek.DayHistorys);
            }
        }
        private async Task<bool> CheckFileExists(string fileName)
        {
            try
            {
                var store = await Windows.Storage.ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
                return true;
            }
            catch
            {
            }
            return false;
        }
        private async Task InsertToastObject(ToastContent toast)
        {
            await toastTable.InsertAsync(toast);
            toasts.Add(toast);
            await RefreshToastItems();
        }
        private async Task RefreshToastItems()
        {
            MobileServiceInvalidOperationException exception = null;
            try
            {
                // This code refreshes the entries in the list view by querying the TodoItems table.
                // The query excludes completed TodoItems
                toasts = await toastTable
                //.Where(user => user.deleted == false)
                .ToCollectionAsync();
            }
            catch (MobileServiceInvalidOperationException e)
            {
                exception = e;
            }

            if (exception != null)
            {
                await new MessageDialog(exception.Message, "Error loading items").ShowAsync();
            }
            else
            {
                //MobileServiceCollection<User, User> usersObj = await userTable
                //.Where(user => user.FirstName!= null)
                //.ToCollectionAsync();
                //names.Clear();
                //foreach (User u in users)
                //{

                //    names.Add(u.FirstName.ToString());
                //}
                //lstUsersList.ItemsSource = names;

                //lstUsersList.ItemsSource = users;
                //this.ButtonSave.IsEnabled = true;
            }
        }
    }
}

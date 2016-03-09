using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ClassLibrary;
using MyActivities;
using MyActivities.Common;
using SQLite.Net;
using SQLite.Net.Async;
using SQLite.Net.Platform.WinRT;
using SQLiteNetExtensions.Extensions;
using SQLiteNetExtensionsAsync.Extensions;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace MyBlackBox
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SensorSetting : Page
    {
        private NavigationHelper navigationHelper;
        private ObservableDictionary defaultViewModel = new ObservableDictionary();

        int SliderLeft = 1, SliderRight = 55;
        int Size = 100, Range = 140;
        Point leftPoint = new Point();
        Point rightPoint = new Point();
        string defaultSetting = "1,55";


        public SensorSetting()
        {
            this.InitializeComponent();

            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
            this.navigationHelper.SaveState += this.NavigationHelper_SaveState;
        }

        /// <summary>
        /// Gets the <see cref="NavigationHelper"/> associated with this <see cref="Page"/>.
        /// </summary>
        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }

        /// <summary>
        /// Gets the view model for this <see cref="Page"/>.
        /// This can be changed to a strongly typed view model.
        /// </summary>
        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="sender">
        /// The source of the event; typically <see cref="NavigationHelper"/>
        /// </param>
        /// <param name="e">Event data that provides both the navigation parameter passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested and
        /// a dictionary of state preserved by this page during an earlier
        /// session.  The state will be null the first time a page is visited.</param>
        private void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache.  Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="sender">The source of the event; typically <see cref="NavigationHelper"/></param>
        /// <param name="e">Event data that provides an empty dictionary to be populated with
        /// serializable state.</param>
        private void NavigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
        }

        #region NavigationHelper registration

        /// <summary>
        /// The methods provided in this section are simply used to allow
        /// NavigationHelper to respond to the page's navigation methods.
        /// <para>
        /// Page specific logic should be placed in event handlers for the  
        /// <see cref="NavigationHelper.LoadState"/>
        /// and <see cref="NavigationHelper.SaveState"/>.
        /// The navigation parameter is available in the LoadState method 
        /// in addition to page state preserved during an earlier session.
        /// </para>
        /// </summary>
        /// <param name="e">Provides data for navigation methods and event
        /// handlers that cannot cancel the navigation request.</param>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;

            await SetSlideBarPosition();

            this.navigationHelper.OnNavigatedTo(e);
        }

        private async Task SetSlideBarPosition()
        {
            file = await storageFolder.CreateFileAsync(filename, CreationCollisionOption.OpenIfExists);

            string text = await FileIO.ReadTextAsync(file);
            int greenZone, redZone;

            if (string.IsNullOrWhiteSpace(text))
            {
                greenZone = SliderLeft;
                redZone = SliderRight;
            }
            else
            {
                var splitText = text.Split(',');
                greenZone = int.Parse(splitText[0]);
                redZone = int.Parse(splitText[1]);
            }

            //Debug.WriteLine(greenZone + " : " + redZone);
            StartSliderBar(greenZone, redZone);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedFrom(e);
        }

        #endregion
        
        private void StartSliderBar(int thumb1Position, int thumb2Position)
        {
            var pos1 = SetPosition(thumb1Position);

            CompositeTransform ct = new CompositeTransform();
            ct.TranslateX = SetPosition(thumb1Position);

            LeftHandle.RenderTransform = ct;
            LeftHandleText.Text = Text(pos1).ToString();

            leftPoint.X = pos1;


            var pos2 = SetPosition(thumb2Position);

            CompositeTransform rCt = new CompositeTransform();
            rCt.TranslateX = pos2;

            RightHandle.RenderTransform = rCt;
            RightHandleText.Text = Text(pos2).ToString();

            rightPoint.X = pos2;

            FillTrackGrid.Width = FillTrack(rightPoint, leftPoint);


            SettingZone settingZone = new SettingZone
            {
                GreenLight = int.Parse(LeftHandleText.Text),
                Redlight = int.Parse(RightHandleText.Text)
            };

            GreenTime.Text = " 0 ~ " + settingZone.GreenLight + " mins";
            YellowTime.Text = " " + settingZone.GreenLight + " ~ " + settingZone.Redlight + " mins";
            RedTime.Text = " " + settingZone.Redlight + " ~ " + "60 mins";

            //Debug.WriteLine(threeLight.GreenLight + " : " + threeLight.Yellowlight + " : " + threeLight.Redlight);

        }
        private void LeftHandle_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var t = (sender as Grid).RenderTransform as CompositeTransform;
            var x = (RightHandle.RenderTransform as CompositeTransform).TranslateX;
            var f = -this.Range;
            var c = x - this.Size * .1;
            double translateVal = Translate(t, e, f, c);
            t.TranslateX = Translate(t, e, f, c);
            LeftHandleText.Text = Text(t.TranslateX).ToString();

            CompositeTransform ct = new CompositeTransform();
            ct.TranslateX = Translate(t, e, f, c);
            FillTrackGrid.RenderTransform = ct;

            //left = Convert.ToInt32(translateVal);

            leftPoint.X = t.TranslateX + e.Delta.Translation.X;
            leftPoint.Y = t.TranslateY + e.Delta.Translation.Y;

            FillTrackGrid.Width = FillTrack(rightPoint, leftPoint);


            SettingZone settingZone = new SettingZone
            {
                GreenLight = int.Parse(LeftHandleText.Text),
                Redlight = int.Parse(RightHandleText.Text)
            };

            GreenTime.Text = " 0 ~ " + settingZone.GreenLight + " mins";
            YellowTime.Text = " " + settingZone.GreenLight + " ~ " + settingZone.Redlight + " mins";
            RedTime.Text = " " + settingZone.Redlight + " ~ " + "60 mins";
            ////Debug.WriteLine(settingZone.GreenLight + " : " + settingZone.Yellowlight + " : " + settingZone.Redlight);

        }

        private void RightHandle_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var t = (sender as Grid).RenderTransform as CompositeTransform;
            var x = (LeftHandle.RenderTransform as CompositeTransform).TranslateX;
            var f = x + this.Size * .1;
            var c = this.Range;
            t.TranslateX = Translate(t, e, f, c);
            RightHandleText.Text = Text(t.TranslateX).ToString();

            double translateVal = Translate(t, e, f, c);
            //right = Convert.ToInt32(translateVal);

            rightPoint.X = t.TranslateX + e.Delta.Translation.X;
            rightPoint.Y = t.TranslateY + e.Delta.Translation.Y;

            FillTrackGrid.Width = FillTrack(rightPoint, leftPoint);


            SettingZone settingZone = new SettingZone
            {
                GreenLight = int.Parse(LeftHandleText.Text),
                Redlight = int.Parse(RightHandleText.Text)
            };

            GreenTime.Text = " 0 ~ " + settingZone.GreenLight + " mins";
            YellowTime.Text = " " + settingZone.GreenLight + " ~ " + settingZone.Redlight + " mins";
            RedTime.Text = " " + settingZone.Redlight + " ~ " + "60 mins";
            ////Debug.WriteLine(settingZone.GreenLight + " : " + settingZone.Yellowlight + " : " + settingZone.Redlight);

        }

        private double Translate(CompositeTransform s, ManipulationDeltaRoutedEventArgs e, double floor, double ceiling)
        {
            var target = s.TranslateX + e.Delta.Translation.X;

            if (target < floor)
                return floor;
            if (target > ceiling)
                return ceiling;
            return target;
        }

        private int Text(double x)
        {
            var p = (x - (-this.Range)) / ((this.Range) - (-this.Range)) * 100d;
            var v = (this.SliderRight - this.SliderLeft) * p / 100d + this.SliderLeft;
            return (int)v;
        }

        public double SetPosition(int value)
        {
            var p = ((value - this.SliderLeft) * 100d) / (this.SliderRight - this.SliderLeft);

            var x = (p * (this.Range - (-this.Range)) / 100d) + (-this.Range);

            return x;
        }

        public double FillTrack(Point A, Point B)
        {

            double a = A.X - B.X;
            double b = A.Y - B.Y;
            double distance = Math.Sqrt(a * a + b * b);

            return distance;
        }

        public const string filename = "setting.txt";
        public StorageFile file = null;
        StorageFolder storageFolder = ApplicationData.Current.LocalFolder;

        private async void BtnSettingSave_OnClick(object sender, RoutedEventArgs e)
        {
            await FileIO.WriteTextAsync(file, LeftHandleText.Text + "," + RightHandleText.Text);

            MessageDialog message = new MessageDialog("The Setting Saved");
            await message.ShowAsync();
            Frame.Navigate(typeof(SensorsDB));
        }

        private void BtnSettingReset_OnClick(object sender, RoutedEventArgs e)
        {
            StartSliderBar(SliderLeft, SliderRight);
        }
    }
}

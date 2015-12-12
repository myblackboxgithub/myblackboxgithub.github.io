using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Services.Maps;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Map
{
    public sealed partial class MainPage : Page
    {
        Geolocator geolocator;
        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {

        }

        private async void btnlocation_Click(object sender, RoutedEventArgs e)
        {
            //getting current location of where you are located 
            geolocator = new Geolocator();
            geolocator.DesiredAccuracyInMeters = 50;

            Geoposition geoposition = await geolocator.GetGeopositionAsync(
                maximumAge: TimeSpan.FromMinutes(5),
                timeout: TimeSpan.FromSeconds(10));

            //pin
            MapIcon myMap = new MapIcon();
            myMap.Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/pin.png"));
            myMap.Title = "You are here!";
            myMap.Location = new Geopoint(new BasicGeoposition()
            {

                Latitude = geoposition.Coordinate.Point.Position.Latitude,
                Longitude = geoposition.Coordinate.Point.Position.Longitude

            });
            myMap.NormalizedAnchorPoint = new Point(0.6, 0.6);
            Map.MapElements.Add(myMap);
            await Map.TrySetViewAsync(myMap.Location, 18D, 0, 0, MapAnimationKind.Bow);


            mySlider.Value = Map.ZoomLevel;
        }


        private async void MyMap_AddressLocation(MapControl sender, MapInputEventArgs args)
        {
            Geopoint location = new Geopoint(args.Location.Position);

            // Getting the pop up window when clicking the pin showing district, town and country of where the device is located.
            MapLocationFinderResult result = await MapLocationFinder.FindLocationsAtAsync(location);

            var locationText = new StringBuilder();

            if (result.Status == MapLocationFinderStatus.Success)
            {
                locationText.AppendLine(result.Locations[0].Address.District + ", " 
                + result.Locations[0].Address.Town + ", " + result.Locations[0].Address.Country);
            }

            MessageBox(locationText.ToString());
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {

            if (Map != null)
                Map.ZoomLevel = e.NewValue;
        }



        private async void MessageBox(string message)
        {
            var dialog = new MessageDialog(message.ToString());
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => await dialog.ShowAsync());
        }
    }
}

        
   


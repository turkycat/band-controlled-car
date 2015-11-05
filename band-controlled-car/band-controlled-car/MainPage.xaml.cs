using Microsoft.Band;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace band_controlled_car
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        IBandClient bandClient;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void Band_Connect( object sender, RoutedEventArgs e )
        {
            BandConnectionStatusText.Text = "Connecting ...";

            try
            {
                // Get the list of Microsoft Bands paired to the phone.
                IBandInfo[] pairedBands = await BandClientManager.Instance.GetBandsAsync();
                if( pairedBands.Length < 1 )
                {
                    BandConnectionStatusText.Text = "This sample app requires a Microsoft Band paired to your device. Also make sure that you have the latest firmware installed on your Band, as provided by the latest Microsoft Health app.";
                    return;
                }

                // Connect to Microsoft Band.
                bandClient = await BandClientManager.Instance.ConnectAsync( pairedBands[0] );
                
                    // Subscribe to Accelerometer data.
                bandClient.SensorManager.Accelerometer.ReadingChanged += Accelerometer_ReadingChanged;
                await bandClient.SensorManager.Accelerometer.StartReadingsAsync();
            }
            catch( Exception ex )
            {
                BandConnectionStatusText.Text = ex.ToString();
            }
        }

        private void Accelerometer_ReadingChanged( object sender, Microsoft.Band.Sensors.BandSensorReadingEventArgs<Microsoft.Band.Sensors.IBandAccelerometerReading> e )
        {
            BandConnectionStatusText.Text = String.Format( "{0}", e.SensorReading.AccelerationX );
        }

        private async void Arduino_Connect( object sender, RoutedEventArgs e )
        {
        }
        }

    
}

using Microsoft.Band;
using Microsoft.Maker.RemoteWiring;
using Microsoft.Maker.Serial;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System.Display;
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
        Controller bandController;

        volatile bool arduinoConnected = false;
        IStream arduinoConnection;
        RemoteDevice arduino;


        private DisplayRequest keepScreenOnRequest;

        public MainPage()
        {
            this.InitializeComponent();

            keepScreenOnRequest = new DisplayRequest();
            keepScreenOnRequest.RequestActive();

            Initialize();
        }

        private async void Initialize()
        {
            while( !arduinoConnected )
            {
                Arduino_Connect();
                await Task.Delay( 10000 );
            }
        }

        private async void Arduino_Connect()
        {
            arduinoConnected = false;
            ArduinoConnectionStatusText.Text = "Connecting...";
            arduinoConnection = new BluetoothSerial( "RNBT-773E" );
            arduino = new RemoteDevice( arduinoConnection );
            arduino.DeviceReady += Arduino_DeviceReady;
            arduino.DeviceConnectionFailed += Arduino_DeviceConnectionFailed;
            arduino.DeviceConnectionLost += Arduino_DeviceConnectionLost;
            arduinoConnection.begin( 115200, SerialConfig.SERIAL_8N1 );
        }

        private void Arduino_Disconnect()
        {
            arduinoConnected = false;
            arduinoConnection.end();
            arduinoConnection = null;
            arduino = null;
        }

        private void Arduino_DeviceReady()
        {
            arduinoConnected = true;

            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                ArduinoConnectionStatusText.Text = "Connected.";

                Debug.WriteLine( "Arduino Connected." );

                InitializeController( arduino );
            } ) );
        }

        private void Arduino_DeviceConnectionFailed( string message )
        {
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                Arduino_Disconnect();
                ArduinoConnectionStatusText.Text = "Connection Failed.";
                Debug.WriteLine( "Arduino Connection Failed." );
            } ) );
        }

        private void Arduino_DeviceConnectionLost( string message )
        {
            arduinoConnected = false;
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                ArduinoConnectionStatusText.Text = "Connection Lost.";
            } ) );
        }

        
        private async Task<bool> InitializeController( RemoteDevice remoteDevice )
        {
            BandConnectionStatusText.Text = "Connecting ...";

            bandController = new BandController( remoteDevice );
            return await bandController.Initialize();
        }
    }
}

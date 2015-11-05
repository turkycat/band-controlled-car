using Microsoft.Band;
using Microsoft.Maker.RemoteWiring;
using Microsoft.Maker.Serial;
using System;
using System.Collections.Generic;
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
        private const double LR_MAG = 0.4;
        private const double FB_MAG = 0.4;
        private const double MAX_ANALOG_VALUE = 255.0;
        private const byte FB_DIRECTION_CONTROL_PIN = 8;
        private const byte FB_MOTOR_CONTROL_PIN = 9;
        private const byte LR_DIRECTION_CONTROL_PIN = 2;
        private const byte LR_MOTOR_CONTROL_PIN = 3;

        private const PinState LEFT = PinState.LOW;
        private const PinState RIGHT = PinState.HIGH;
        private const PinState FORWARD = PinState.LOW;
        private const PinState REVERSE = PinState.HIGH;

        private Turn turn;
        private Direction direction;

        IBandClient bandClient;

        volatile bool arduinoConnected = false;
        IStream arduinoConnection;
        RemoteDevice arduino;

        private enum Turn
        {
            none,
            left,
            right
        }

        private enum Direction
        {
            none,
            forward,
            reverse
        }

        private DisplayRequest keepScreenOnRequest;

        public MainPage()
        {
            this.InitializeComponent();
            turn = Turn.none;
            direction = Direction.none;
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

                BandConnectionStatusText.Text = "Connected.";

                // Subscribe to Accelerometer data.
                //var intervals = bandClient.SensorManager.Accelerometer.SupportedReportingIntervals;
                bandClient.SensorManager.Accelerometer.ReportingInterval = bandClient.SensorManager.Accelerometer.SupportedReportingIntervals.Last();
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
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                if( arduinoConnected )
                {
                    //Y is the left/right tilt, while X is the fwd/rev tilt
                    double lr = e.SensorReading.AccelerationY;
                    double fb = e.SensorReading.AccelerationX;

                    handleTurn( lr );
                    handleDirection( fb );
                }

                XYZText.Text = String.Format( "x: {0}\ny: {1}\nz: {2}", e.SensorReading.AccelerationX, e.SensorReading.AccelerationY, e.SensorReading.AccelerationZ );
            } ) );

        }

        private async void Arduino_Connect( object sender, RoutedEventArgs e )
        {
            if( arduinoConnected )
            {
                //disconnect
                Arduino_Disconnect();
                ArduinoConnectionStatusText.Text = "Disconnected.";
            }
            else
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

                arduino.pinMode( LR_DIRECTION_CONTROL_PIN, PinMode.OUTPUT );
                arduino.pinMode( FB_DIRECTION_CONTROL_PIN, PinMode.OUTPUT );
                arduino.pinMode( LR_MOTOR_CONTROL_PIN, PinMode.PWM );
                arduino.pinMode( FB_MOTOR_CONTROL_PIN, PinMode.PWM );
            } ) );
        }

        private void Arduino_DeviceConnectionFailed( string message )
        {
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                Arduino_Disconnect();
                ArduinoConnectionStatusText.Text = "Connection Failed.";
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

        private void handleTurn( double lr )
        {
            //left and right turns work best using digital signals

            if( lr < -LR_MAG )
            {
                //if we've switched directions, we need to be careful about how we switch
                if( turn != Turn.left )
                {
                    //stop motor & set direction left
                    arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.LOW );
                    arduino.digitalWrite( LR_DIRECTION_CONTROL_PIN, LEFT );
                }

                //start the motor by setting the pin high
                arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.HIGH );
                turn = Turn.left;
            }
            else if( lr > LR_MAG )
            {
                if( turn != Turn.right )
                {
                    //stop motor & set direction right
                    arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.LOW );
                    arduino.digitalWrite( LR_DIRECTION_CONTROL_PIN, RIGHT );
                }

                //start the motor by setting the pin high
                arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.HIGH );
                turn = Turn.right;
            }
            else
            {
                //stop the motor
                arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.LOW );
                turn = Turn.none;
            }
        }

        private void handleDirection( double fb )
        {
            /*
             * The neutral state is anywhere from (-0.5, 0), so that the phone can be held like a controller, at a moderate angle.
             * This is because holding the phone at an angle is natural, tilting back to -1.0 is easy, while it feels awkward to tilt the phone
             *  forward beyond 0.5 Therefore, reverse is from [-1.0, -0.5] and forward is from [0, 0.5].
             *
             * if the tilt goes beyond -0.5 in the negative direction the phone is being tilted backwards, and the car will start to reverse.
             * if the tilt goes beyond 0 in the positive direction the phone is being tilted forwards, and the car will start to move forward.
             */

            if( fb < -FB_MAG )
            {
                //reading is less than the negative magnitude, the phone is being tilted back and the car should reverse
                double weight = -( fb + FB_MAG );
                byte analogVal = mapWeight( weight );

                if( direction != Direction.reverse )
                {
                    //stop motor & set direction forward
                    arduino.analogWrite( FB_MOTOR_CONTROL_PIN, 0 );
                    arduino.digitalWrite( FB_DIRECTION_CONTROL_PIN, REVERSE );
                }

                //start the motor by setting the pin to the appropriate analog value
                arduino.analogWrite( FB_MOTOR_CONTROL_PIN, analogVal );
                direction = Direction.reverse;
            }
            else if( fb > 0 )
            {
                //reading is greater than zero, the phone is being tilted forward and the car should move forward
                byte analogVal = mapWeight( fb );

                if( direction != Direction.forward )
                {
                    //stop motor & set direction forward
                    arduino.analogWrite( FB_MOTOR_CONTROL_PIN, 0 );
                    arduino.digitalWrite( FB_DIRECTION_CONTROL_PIN, FORWARD );
                }

                //start the motor by setting the pin to the appropriate analog value
                arduino.analogWrite( FB_MOTOR_CONTROL_PIN, analogVal );
                direction = Direction.forward;
            }
            else
            {
                //reading is in the neutral zone (between -FB_MAG and 0) and the car should stop/idle
                arduino.analogWrite( FB_MOTOR_CONTROL_PIN, 0 );
                direction = Direction.none;
            }
        }

        private byte mapWeight( double weight )
        {
            //the value should be [0, 0.5], but we want to clamp the value between [0, 1]
            weight = Math.Max( Math.Min( weight * 2, 1.0 ), 0.0 );
            return (byte)( weight * MAX_ANALOG_VALUE );
        }
    }
}

using Microsoft.Band;
using Microsoft.Maker.RemoteWiring;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace band_controlled_car
{
    /**
     * To use this class, verify that your appxmanifest has the following capabilities:
     *    <Capabilities>
     *       <DeviceCapability Name="proximity" />
     *       <DeviceCapability Name="bluetooth.rfcomm">
     *         <Device Id="any">
     *         <!-- Used by the Microsoft Band SDK -->
     *        <Function Type="serviceId:A502CA9A-2BA5-413C-A4E0-13804E47B38F" />
     *         <!-- Used by the Microsoft Band SDK -->
     *         <Function Type="serviceId:C742E1A2-6320-5ABC-9643-D206C677E580" />
     *         <!-- Used by RemoteDevice -->
     *         <Function Type="name:serialPort" />
     *       </Device>
     *      </DeviceCapability>
     *    </Capabilities>
     */

    public class BandController : Controller
    {
        //connection mechanisms
        private const int MAX_CONNECTION_ATTEMPTS = 5;

        //movement magnitudes
        private const double LR_MAG = 0.3;
        private const double FB_MAG = 0.5;

        //constants for controlling GPIO pins
        private const double MAX_ANALOG_VALUE = 255.0;
        private const byte FB_DIRECTION_CONTROL_PIN = 8;
        private const byte FB_MOTOR_CONTROL_PIN = 9;
        private const byte LR_DIRECTION_CONTROL_PIN = 2;
        private const byte LR_MOTOR_CONTROL_PIN = 3;

        //four directions for motor pins
        private const PinState LEFT = PinState.LOW;
        private const PinState RIGHT = PinState.HIGH;
        private const PinState FORWARD = PinState.LOW;
        private const PinState REVERSE = PinState.HIGH;
        
        //current movement members
        private Turn turn;
        private Direction direction;

        //Microsoft Band members
        IBandClient bandClient;
        private bool connected;

        public BandController( RemoteDevice device ) : base( device )
        {
            connected = false;
            turn = Turn.none;
            direction = Direction.none;

            Arduino.pinMode( LR_DIRECTION_CONTROL_PIN, PinMode.OUTPUT );
            Arduino.pinMode( FB_DIRECTION_CONTROL_PIN, PinMode.OUTPUT );
            device.pinMode( LR_MOTOR_CONTROL_PIN, PinMode.PWM );
            device.pinMode( FB_MOTOR_CONTROL_PIN, PinMode.PWM );
        }

        public override async Task<bool> Initialize()
        {
            connected = false;
            for( int i = 0; !connected && i < MAX_CONNECTION_ATTEMPTS; ++i )
            {
                connected = await Band_Connect();
            }
            return connected;
        }


        private async Task<bool> Band_Connect()
        {
            try
            {
                // Get the list of Microsoft Bands paired to the phone.
                IBandInfo[] pairedBands = await BandClientManager.Instance.GetBandsAsync();
                if( pairedBands.Length < 1 )
                {
                    Debug.WriteLine( "Microsoft Band not found. Verify your band is paired to this device and has the latest firmware." );
                    return false;
                }

                // Connect to Microsoft Band.
                bandClient = await BandClientManager.Instance.ConnectAsync( pairedBands[0] );

                Debug.WriteLine( "Band Connected." );

                // Subscribe to Accelerometer data.
                //var intervals = bandClient.SensorManager.Accelerometer.SupportedReportingIntervals;
                bandClient.SensorManager.Accelerometer.ReportingInterval = bandClient.SensorManager.Accelerometer.SupportedReportingIntervals.Last();
                bandClient.SensorManager.Accelerometer.ReadingChanged += BandAccelerometer_ReadingChanged;
                await bandClient.SensorManager.Accelerometer.StartReadingsAsync();

                Debug.WriteLine( "Accelerometer samples started." );
            }
            catch( Exception )
            {
                return false;
            }
            return true;
        }



        private void BandAccelerometer_ReadingChanged( object sender, Microsoft.Band.Sensors.BandSensorReadingEventArgs<Microsoft.Band.Sensors.IBandAccelerometerReading> e )
        {
            //Y is the left/right tilt, while X is the fwd/rev tilt
            double lr = -e.SensorReading.AccelerationZ;
            double fb = e.SensorReading.AccelerationX;

            HandleTurn( lr );
            HandleDirection( fb );
        }

        private void HandleTurn( double lr )
        {
            //left and right turns work best using digital signals

            if( lr < -LR_MAG )
            {
                //if we've switched directions, we need to be careful about how we switch
                if( turn != Turn.left )
                {
                    //stop motor & set direction left
                    Arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.LOW );
                    Arduino.digitalWrite( LR_DIRECTION_CONTROL_PIN, LEFT );
                }

                //start the motor by setting the pin high
                Arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.HIGH );
                turn = Turn.left;
            }
            else if( lr > LR_MAG )
            {
                if( turn != Turn.right )
                {
                    //stop motor & set direction right
                    Arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.LOW );
                    Arduino.digitalWrite( LR_DIRECTION_CONTROL_PIN, RIGHT );
                }

                //start the motor by setting the pin high
                Arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.HIGH );
                turn = Turn.right;
            }
            else
            {
                //stop the motor
                Arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.LOW );
                turn = Turn.none;
            }
        }

        private void HandleDirection( double fb )
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
                    Arduino.analogWrite( FB_MOTOR_CONTROL_PIN, 0 );
                    Arduino.digitalWrite( FB_DIRECTION_CONTROL_PIN, REVERSE );
                }

                //start the motor by setting the pin to the appropriate analog value
                Arduino.analogWrite( FB_MOTOR_CONTROL_PIN, analogVal );
                direction = Direction.reverse;
            }
            else if( fb > 0 )
            {
                //reading is greater than zero, the phone is being tilted forward and the car should move forward
                byte analogVal = mapWeight( fb );

                if( direction != Direction.forward )
                {
                    //stop motor & set direction forward
                    Arduino.analogWrite( FB_MOTOR_CONTROL_PIN, 0 );
                    Arduino.digitalWrite( FB_DIRECTION_CONTROL_PIN, FORWARD );
                }

                //start the motor by setting the pin to the appropriate analog value
                Arduino.analogWrite( FB_MOTOR_CONTROL_PIN, analogVal );
                direction = Direction.forward;
            }
            else
            {
                //reading is in the neutral zone (between -FB_MAG and 0) and the car should stop/idle
                Arduino.analogWrite( FB_MOTOR_CONTROL_PIN, 0 );
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

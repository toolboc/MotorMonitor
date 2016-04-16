// Copyright (c) Microsoft. All rights reserved.

using System;
using Windows.Devices.Gpio;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Devices.Pwm;
using System.Diagnostics;

namespace MotorMonitor
{
    public sealed partial class MainPage : Page
    {
        private const int REED_SWITCH = 18; //28 for DragonBoard 410c //18 for RPi;
        private GpioPin reedSwitch;

        double wheelDiameterInMeters = .1016; //4 inches
        double maxExpectedSpeedInMetersPerSecond = 3.5; //allows for filtering out wild values due to EMF

        double accumulator;//calculation of moving average http://stackoverflow.com/questions/10990618/calculate-rolling-moving-average-in-c-or-c 
        double alpha = 0.15; //The closer alpha is to 1.0, the faster the moving average updates in response to new values
        
        double mph;

        DateTime currTime;
        DateTime lastTime;

        double speedInMetersPerSecond = 0;

        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush grayBrush = new SolidColorBrush(Windows.UI.Colors.LightGray);


        //private const int LED_PIN = 33;
        //private GpioPin ledPin;

        private const int MOTOR_PIN = 0; //33 for DragonBoard 410c //23 for RPi //0 if using hardware PWM;
        PwmPin motorPin;

        PwmController pwmController;

        DispatcherTimer timer;

        public MainPage()
        {
            InitializeComponent();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1000);
            timer.Tick += Timer_Tick; ;

            InitGPIO();
        }

        private async void Timer_Tick(object sender, object e)
        {
            if((DateTime.Now - lastTime).TotalSeconds > timer.Interval.TotalSeconds)
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    CompositionGauge.Value = 0;
                }); 
        }

        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                GpioStatus.Text = "There is no GPIO controller on this device.";
                return;
            }

            reedSwitch = gpio.OpenPin(REED_SWITCH);

            // Check if input pull-up resistors are supported
            if (reedSwitch.IsDriveModeSupported(GpioPinDriveMode.InputPullUp))
                reedSwitch.SetDriveMode(GpioPinDriveMode.InputPullUp);
            else
                reedSwitch.SetDriveMode(GpioPinDriveMode.Input);

            // Set a debounce timeout to filter out switch bounce noise
            //reedSwitch.DebounceTimeout = TimeSpan.FromMilliseconds(50);

            // Register for the ValueChanged event so our reedSwitch_ValueChanged 
            // function is called when the button is pressed
            reedSwitch.ValueChanged += reedSwitch_ValueChanged;

            GpioStatus.Text = "GPIO pins initialized correctly.";

            //ledPin = gpio.OpenPin(LED_PIN);
            //ledPin.Write(GpioPinValue.High);
            InitMotor();

            timer.Start();
        }

        private async void InitMotor()
        {
            //pwmController = (await PwmController.GetControllersAsync(PwmSoftware.PwmProviderSoftware.GetPwmProvider()))[0];
            pwmController =  (await PwmController.GetControllersAsync(PwmPCA9685.PwmProviderPCA9685.GetPwmProvider()))[0];
            pwmController.SetDesiredFrequency(1000);
            motorPin = pwmController.OpenPin(MOTOR_PIN);
            motorPin.SetActiveDutyCyclePercentage(0);
            motorPin.Start();
        }

        private async void reedSwitch_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs e)
        {

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
               GpioStatus.Text = "";
            });

            if (e.Edge == GpioPinEdge.RisingEdge)
            {

                currTime = DateTime.Now;


                if (lastTime != null && lastTime != currTime)
                {
                    speedInMetersPerSecond = (Math.PI * wheelDiameterInMeters) / ((currTime - lastTime).TotalSeconds);

                    if (speedInMetersPerSecond < maxExpectedSpeedInMetersPerSecond) //discard magnetic interference
                        mph = speedInMetersPerSecond * 2.23693629;

                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                 {
                     ledEllipse.Fill = redBrush;
                     //GpioStatus.Text = "Switch Active";

                     //Begin calculation of moving average http://stackoverflow.com/questions/10990618/calculate-rolling-moving-average-in-c-or-c 
                     accumulator = (alpha * mph) + (1.0 - alpha) * accumulator;

                     CompositionGauge.Value = accumulator;
                 });

                 lastTime = currTime;

                }
                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        ledEllipse.Fill = grayBrush;
                        //GpioStatus.Text = "Switch Released";
                    });

                }

        }

        private void Slider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            motorPin.SetActiveDutyCyclePercentage(e.NewValue / 100);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using Windows.ApplicationModel;
using Windows.Devices.Gpio;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RPiVoice
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // grammar File
        private const string SRGS_FILE = "Grammar\\grammar.xml";
        // RED Led Pin
        private const int RED_LED_PIN = 5;
        // GREEN Led Pin
        private const int GREEN_LED_PIN = 6;
        // Bedroom Light Pin
        private const int BEDROOM_LIGHT_PIN = 13;
        // Tag TARGET
        private const string TAG_TARGET = "target";
        // Tag CMD
        private const string TAG_CMD = "cmd";
        // Tag Device
        private const string TAG_DEVICE = "device";
        // On State
        private const string STATE_ON = "ON";
        // Off State
        private const string STATE_OFF = "OFF";
        // LED Device
        private const string DEVICE_LED = "LED";
        // Light Device
        private const string DEVICE_LIGHT = "LIGHT";
        // Red Led
        private const string COLOR_RED = "RED";
        // Green Led
        private const string COLOR_GREEN = "GREEN";
        // Bedroom
        private const string TARGET_BEDROOM = "BEDROOM";
        // Porch
        private const string TARGET_PORCH = "PORCH";
        
        // Speech Recognizer
        private SpeechRecognizer recognizer;
        // GPIO 
        private static GpioController gpio = null;
        // GPIO Pin for RED Led
        private static GpioPin redPin = null;
        // GPIO Pin for GREEN Led
        private static GpioPin greenPin = null;
        // GPIO Pin for Bedroom Light Led
        private static GpioPin bedroomLightPin = null;

        public MainPage()
        {
            this.InitializeComponent();

            Unloaded += MainPage_Unloaded;

            // Initialize Recognizer
            initializeSpeechRecognizer();
            // Initialize GPIO controller and pins
            initializeGPIO();
        }

        private void initializeGPIO()
        {
            // Initialize GPIO controller
            gpio = GpioController.GetDefault();

            // // Initialize GPIO Pins
            redPin = gpio.OpenPin(RED_LED_PIN);
            greenPin = gpio.OpenPin(GREEN_LED_PIN);
            bedroomLightPin = gpio.OpenPin(BEDROOM_LIGHT_PIN);

            redPin.SetDriveMode(GpioPinDriveMode.Output);
            greenPin.SetDriveMode(GpioPinDriveMode.Output);
            bedroomLightPin.SetDriveMode(GpioPinDriveMode.Output);

            // Write low initially, this step is not needed
            redPin.Write(GpioPinValue.Low);
            greenPin.Write(GpioPinValue.Low);
            bedroomLightPin.Write(GpioPinValue.Low);
        }

        // Release resources, stop recognizer, release pins, etc...
        private async void MainPage_Unloaded(object sender, object args)
        {
            // Stop recognizing
            await recognizer.ContinuousRecognitionSession.StopAsync();

            // Release pins
            redPin.Dispose();
            greenPin.Dispose();
            recognizer.Dispose();

            gpio = null;
            redPin = null;
            greenPin = null;
            recognizer = null;
        }

        // Initialize Speech Recognizer and start async recognition
        private async void initializeSpeechRecognizer()
        {
            // Initialize recognizer
            recognizer = new SpeechRecognizer();

            // Set event handlers
            recognizer.StateChanged += RecognizerStateChanged;
            recognizer.ContinuousRecognitionSession.ResultGenerated += RecognizerResultGenerated;

            // Load Grammar file constraint
            string fileName = String.Format(SRGS_FILE);
            StorageFile grammarContentFile = await Package.Current.InstalledLocation.GetFileAsync(fileName);

            SpeechRecognitionGrammarFileConstraint grammarConstraint = new SpeechRecognitionGrammarFileConstraint(grammarContentFile);

            // Add to grammar constraint
            recognizer.Constraints.Add(grammarConstraint);

            // Compile grammar
            SpeechRecognitionCompilationResult compilationResult = await recognizer.CompileConstraintsAsync();

            Debug.WriteLine("Status: " + compilationResult.Status.ToString());

            // If successful, display the recognition result.
            if (compilationResult.Status == SpeechRecognitionResultStatus.Success)
            {
                Debug.WriteLine("Result: " + compilationResult.ToString());

                await recognizer.ContinuousRecognitionSession.StartAsync();
            }
            else
            {
                Debug.WriteLine("Status: " + compilationResult.Status);
            }
        }

        // Recognizer generated results
        private void RecognizerResultGenerated(SpeechContinuousRecognitionSession session, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // Output debug strings
            Debug.WriteLine(args.Result.Status);
            Debug.WriteLine(args.Result.Text);

            int count = args.Result.SemanticInterpretation.Properties.Count;

            Debug.WriteLine("Count: " + count);
            Debug.WriteLine("Tag: " + args.Result.Constraint.Tag);

            // Check for different tags and initialize the variables
            String target = args.Result.SemanticInterpretation.Properties.ContainsKey(TAG_TARGET) ?
                            args.Result.SemanticInterpretation.Properties[TAG_TARGET][0].ToString() :
                            "";

            String cmd = args.Result.SemanticInterpretation.Properties.ContainsKey(TAG_CMD) ?
                            args.Result.SemanticInterpretation.Properties[TAG_CMD][0].ToString() :
                            "";

            String device = args.Result.SemanticInterpretation.Properties.ContainsKey(TAG_DEVICE) ?
                            args.Result.SemanticInterpretation.Properties[TAG_DEVICE][0].ToString() :
                            "";

            // Whether state is on or off
            bool isOn = cmd.Equals(STATE_ON);

            Debug.WriteLine("Target: " + target + ", Command: " + cmd + ", Device: " + device);

            // First check which device the user refers to
            if (device.Equals(DEVICE_LED))
            {
                // Check what color is specified
                if (target.Equals(COLOR_RED))
                {
                    Debug.WriteLine("RED LED " + (isOn ? STATE_ON : STATE_OFF));

                    // Turn on the Red LED
                    WriteGPIOPin(redPin, isOn ? GpioPinValue.High : GpioPinValue.Low);
                }
                else if (target.Equals(COLOR_GREEN))
                {
                    Debug.WriteLine("GREEN LED " + (isOn ? STATE_ON : STATE_OFF));

                    // Turn on the Green LED
                    WriteGPIOPin(greenPin, isOn ? GpioPinValue.High : GpioPinValue.Low);
                }
                else
                {
                    Debug.WriteLine("Unknown Target");
                }
            }
            else if (device.Equals(DEVICE_LIGHT))
            {
                // Check target location
                if (target.Equals(TARGET_BEDROOM))
                {
                    Debug.WriteLine("BEDROOM LIGHT " + (isOn ? STATE_ON : STATE_OFF));
                    
                    // Turn on the bedroom light
                    WriteGPIOPin(bedroomLightPin, isOn ? GpioPinValue.High : GpioPinValue.Low);
                }
                else if (target.Equals(TARGET_PORCH))
                {
                    Debug.WriteLine("PORCH LIGHT " + (isOn ? STATE_ON : STATE_OFF));

                    // Insert code to control Porch light
                }
                else
                {
                    Debug.WriteLine("Unknown Target");
                }
            }
            else
            {
                Debug.WriteLine("Unknown Device");
            }

            /*foreach (KeyValuePair<String, IReadOnlyList<string>> child in args.Result.SemanticInterpretation.Properties)
            {
                Debug.WriteLine(child.Key + " = " + child.Value.ToString());

                foreach (String val in child.Value)
                {
                    Debug.WriteLine("Value = " + val);
                }
            }*/
        }

        // Recognizer state changed
        private void RecognizerStateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            Debug.WriteLine("Speech recognizer state: " + args.State.ToString());
        }
        
        // Control Gpio Pins
        private void WriteGPIOPin(GpioPin pin, GpioPinValue value)
        {
            pin.Write(value);
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using GrovePi.Sensors;
using GrovePi;
using System.Threading.Tasks;
using GrovePi.I2CDevices;
// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409
using Microsoft.Azure.Devices.Client;
using System.Text;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;

namespace BMC.Aquascape2
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private DispatcherTimer timer;
        private DispatcherTimer timer2;

        IUltrasonicRangerSensor distance = DeviceFactory.Build.UltraSonicSensor(Pin.DigitalPin7);

        private static DeviceClient s_deviceClient;

        IRgbLcdDisplay display = DeviceFactory.Build.RgbLcdDisplay();

        IRelay WaterInRelay = DeviceFactory.Build.Relay(Pin.DigitalPin3);

        IRelay WaterOutRelay = DeviceFactory.Build.Relay(Pin.DigitalPin4);

        private readonly static string s_connectionString = "HostName=FreeDeviceHub.azure-devices.net;DeviceId=aquascape;SharedAccessKey=CbUvVt51qS81B53rFjmm5sx26ymhiJ1djZ4h+BS0Xw4=";

        bool IsConnected = false;
        public MainPage()
        {
            this.InitializeComponent();
            Setup();
            this.timer = new DispatcherTimer();
            this.timer.Interval = TimeSpan.FromMilliseconds(5*60*1000); //1 minutes
            this.timer.Tick += this.OnTick;
            this.timer.Start();

            this.timer2 = new DispatcherTimer();
            this.timer2.Interval = TimeSpan.FromMilliseconds(2000); //1 minutes
            this.timer2.Tick += this.OnTick2;
            this.timer2.Start();
        }
        private static async void SendDeviceToCloudMessagesAsync(dynamic data)
        {
            var message = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(data)));

            // Add a custom application property to the message.
            // An IoT hub can filter on these properties without access to the message body.
            //message.Properties.Add("temperatureAlert", (data.Temp > 40) ? "true" : "false");

            // Send the telemetry message
            await s_deviceClient.SendEventAsync(message);
            Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, "ok");

        }
        private async void OnTick(object sender, object e)
        {
            try
            {
                if (IsConnected)
                {
                    var item = new  {WaterDistance = distance.MeasureInCentimeters(), LocalTime = DateTime.Now };
                    SendDeviceToCloudMessagesAsync(item);
                    display.SetText($"water:{distance.MeasureInCentimeters()} cm");
                }
                else
                {
                    Setup();
                }
            }
            catch
            {
                IsConnected = false;
            }
        }
        private async void OnTick2(object sender, object e)
        {
            try
            {

                display.SetText($"water: {distance.MeasureInCentimeters()} cm");

            }
            catch
            {

            }
        }
        void Setup()
        {
            try
            {
                if (!IsConnected)
                {
                    if (s_deviceClient != null)
                    {
                        s_deviceClient.Dispose();
                    }
                    // Connect to the IoT hub using the MQTT protocol
                    s_deviceClient = DeviceClient.CreateFromConnectionString(s_connectionString, TransportType.Mqtt);
                    s_deviceClient.SetMethodHandlerAsync("DoAction", DoAction, null).Wait();
                    //SendDeviceToCloudMessagesAsync();
               
                    display.SetBacklightRgb(0, 200, 100);
                    display.SetText("Device is Ready");
                    IsConnected = true;
                }
                
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message + "_" + ex.StackTrace);
            }


        }
        // Handle the direct method call
        private async Task<MethodResponse> DoAction(MethodRequest methodRequest, object userContext)
        {
            var data = Encoding.UTF8.GetString(methodRequest.Data);
            var action = JsonConvert.DeserializeObject<DeviceAction>(data);
            // Check the payload is a single integer value
            if (action != null)
            {
                /*
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Telemetry interval set to {0} seconds", data);
                Console.ResetColor();
                */
                switch (action.ActionName)
                {
                    case "WaterIn":
                        {
                            var res = bool.Parse(action.Params[0]);
                            WaterInRelay.ChangeState(res ? SensorStatus.On : SensorStatus.Off);
                            display.SetText("WATER IN :" + res);
                        }
                        break;
                    case "WaterOut":
                        {
                            var res = bool.Parse(action.Params[0]);
                            WaterOutRelay.ChangeState(res ? SensorStatus.On : SensorStatus.Off);
                            display.SetText("WATER OUT :" + res);
                        }
                        break;
                }
                // Acknowlege the direct method call with a 200 success message
                string result = "{\"result\":\"Executed direct method: " + methodRequest.Name + "\"}";
                return new MethodResponse(Encoding.UTF8.GetBytes(result), 200);
            }
            else
            {
                // Acknowlege the direct method call with a 400 error message
                string result = "{\"result\":\"Invalid parameter\"}";
                return new MethodResponse(Encoding.UTF8.GetBytes(result), 400);
            }
        }
        public IPAddress GetIPAddress()
        {
            List<string> IpAddress = new List<string>();
            var Hosts = Windows.Networking.Connectivity.NetworkInformation.GetHostNames().ToList();
            foreach (var Host in Hosts)
            {
                string IP = Host.DisplayName;
                IpAddress.Add(IP);
            }
            IPAddress address = IPAddress.Parse(IpAddress.Last());
            return address;
        }
        void Testing()
        {
            while (true)
            {
                
                // First, output to the LCD Display.
                display.SetText("Waktu - "+DateTime.Now).SetBacklightRgb(255, 50, 255);
                WaterInRelay.ChangeState(SensorStatus.On);                     // Turn the relay on.
                WaterOutRelay.ChangeState(SensorStatus.On);                     // Turn the relay on.
                System.Diagnostics.Debug.WriteLine("Relay is On.");    // Write something to debug.
                Task.Delay(2000).Wait();
                WaterInRelay.ChangeState(SensorStatus.Off);                     // Turn the relay on.
                WaterOutRelay.ChangeState(SensorStatus.Off);                     // Turn the relay on.
                System.Diagnostics.Debug.WriteLine("Relay is Off.");    // Write something to debug.
                Task.Delay(2000).Wait();
            }
        }
    }
    public class DeviceAction
    {
        public string ActionName { get; set; }
        public string[] Params { get; set; }
    }
}

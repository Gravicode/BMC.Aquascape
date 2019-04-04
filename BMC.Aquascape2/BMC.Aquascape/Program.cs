using BMC.Aquascape.Model;
using BMC.LowLevelDrivers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Abstractions;
//using Unosquare.RaspberryIO.Gpio;
using Unosquare.WiringPi;

namespace BMC.Aquascape
{
    class Program
    {
        static IGpioPin Relay1;
        static IGpioPin Relay2;
        static IGpioPin Limit1;
        static IGpioPin Limit2;
        static bool Relay1Status;
        static bool Relay2Status;
        static void Main(string[] args)
        {
            try
            {

                Pi.Init<BootstrapWiringPi>();
                Console.WriteLine(">> Init mqtt");
                MqttService mqtt = new MqttService();
                DS1307 jam = new DS1307();
                TempSensor tempSensor = new TempSensor();
                var nowDate = DateTime.Now;
                Console.WriteLine("Device Date :" + nowDate);
                jam.SetDateAsync(nowDate).GetAwaiter().GetResult();
                Console.WriteLine("TGL RTC:" + jam.GetDateAsync().GetAwaiter().GetResult().ToString());
                ADS1115_PY analog = new ADS1115_PY();
                Relay1 = Pi.Gpio[BcmPin.Gpio06];//Pi.Gpio.Pin06;
                Relay2 = Pi.Gpio[BcmPin.Gpio13];//Pi.Gpio.Pin13;
                Limit1 = Pi.Gpio[BcmPin.Gpio19];//Pi.Gpio.Pin19;
                Limit2 = Pi.Gpio[BcmPin.Gpio26];//Pi.Gpio.Pin26;
                Relay1Status = true;
                Relay2Status = true;

                WriteDigital(Relay1, Relay1Status);
                WriteDigital(Relay2, Relay2Status);
                mqtt.CommandReceived += (string Message) =>
                {
                    Task.Run(async () => { await DoAction(Message); });
                };
                var INTERVAL = int.Parse(ConfigurationManager.AppSettings["Interval"]);
                //analog.Start();
                /*
                analog.ChannelChanged += (object sender, ChannelReadingDone e) =>
                {
                    Console.WriteLine($">> channel {e.Channel} : {e.RawValue}"); 
                };*/
                while (true)
                {
                    /*
                    for (int i = 0; i < 4; i++)
                    {
                        Console.WriteLine($"A{i} = {analog.read_adc(i)}");
                    }*/
                    var sensor = new DeviceData() { LimitSwitch1 = ReadDigital(Limit1), LimitSwitch2= ReadDigital(Limit2), Relay1 = Relay1Status, Relay2 = Relay2Status, TDS1 = analog.read_adc(0),
                     TDS2 = analog.read_adc(1), Temp = tempSensor.Read()
                    };
                    Console.WriteLine(">>------------------>>");
                    Console.WriteLine($"TDS 1: {sensor.TDS1}");
                    Console.WriteLine($"TDS 2: {sensor.TDS2}");
                    Console.WriteLine($"Relay 1: {sensor.Relay1}");
                    Console.WriteLine($"Relay 2: {sensor.Relay2}");
                    Console.WriteLine($"Limit 1: {sensor.LimitSwitch1}");
                    Console.WriteLine($"Limit 2: {sensor.LimitSwitch2}");
                    Console.WriteLine($"Temp: {sensor.Temp}");
                    mqtt.PublishMessage(JsonConvert.SerializeObject(sensor));
                   
                    Thread.Sleep(INTERVAL);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

        }
        static async Task<string> DoAction(string Data)
        {
            //var data = Encoding.UTF8.GetString(Data);
            var action = JsonConvert.DeserializeObject<DeviceAction>(Data);
            // Check the payload is a single integer value
            if (action != null)
            {
                switch (action.ActionName)
                {
                    case "PlaySound":

                        break;
                    case "Relay2":
                        {
                            var State = Convert.ToBoolean(action.Params[0]);
                            WriteDigital(Relay2, State);
                            Relay2Status = State;
                            Console.WriteLine($"RELAY 2 : {State}");
                        }

                        break;
                    case "Relay1":
                        {
                            var State = Convert.ToBoolean(action.Params[0]);
                            WriteDigital(Relay1, State);
                            Relay1Status = State;
                            Console.WriteLine($"RELAY 1 : {State}");
                        }
                        
                        break;

                }
                // Acknowlege the direct method call with a 200 success message
                string result = "{\"result\":\"Executed direct method: " + action.ActionName + "\"}";
                return result;
                //return new MethodResponse(Encoding.UTF8.GetBytes(result), 200);
            }
            else
            {
                // Acknowlege the direct method call with a 400 error message
                string result = "{\"result\":\"Invalid parameter\"}";
                return result;
                //return new MethodResponse(Encoding.UTF8.GetBytes(result), 400);
            }
        }




        /// <summary>
        /// Write the given value to the given pin.
        /// </summary>
        /// <param name="pin">The pin to set.</param>
        /// <param name="state">The new state of the pin.</param>
        public static void WriteDigital(IGpioPin gpioPin, bool state)
        {
            if (gpioPin.PinMode != GpioPinDriveMode.Output)
                gpioPin.PinMode = GpioPinDriveMode.Output;

            gpioPin.Write(state ? GpioPinValue.High : GpioPinValue.Low);
        }

        /// <summary>
        /// Reads the current state of the given pin.
        /// </summary>
        /// <param name="pin">The pin to read.</param>
        /// <returns>True if high, false is low.</returns>
        public static bool ReadDigital(IGpioPin gpioPin)
        {

            if (gpioPin.PinMode != GpioPinDriveMode.Input)
                gpioPin.PinMode = GpioPinDriveMode.Input;

            return gpioPin.Read() == true;
        }
    }

    
}

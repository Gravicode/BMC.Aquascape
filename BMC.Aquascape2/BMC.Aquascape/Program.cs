using BMC.Aquascape.Model;
using BMC.LowLevelDrivers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO.Ports;
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

        private static string[] _dataInLora;
        private static string rx;
        static SimpleSerial UART = null;
        static void PrintToLcd(string Message)
        {
            Console.WriteLine($"{DateTime.Now.ToString("dd/MM/yy HH:mm:ss")} >> {Message}");
        }

        static void Main(string[] args)
        {
            try
            {
                #region lora
                string SerialPortName = ConfigurationManager.AppSettings["Port"];
                UART = new SimpleSerial(SerialPortName, 57600);
                UART.ReadTimeout = 0;
                /*
                UART.ReadBufferSize = 1024;
                UART.WriteBufferSize = 1024;
                UART.BaudRate = 38400;
                UART.DataBits = 8;
                UART.Parity = Parity.None;
                UART.StopBits = StopBits.One;
                */
                UART.DataReceived += UART_DataReceived;
                Console.WriteLine("57600");
                Console.WriteLine("RN2483 Test");

                var reset = Pi.Gpio[BcmPin.Gpio06]; //pin 6 
                var reset2 =  Pi.Gpio[BcmPin.Gpio06]; //pin 3
                #endregion

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
                #region lora
                reset.Write(true);
                reset2.Write(true);

                Thread.Sleep(100);
                reset.Write(false);
                reset2.Write(false);

                Thread.Sleep(100);
                reset.Write(true);
                reset2.Write(true);

                Thread.Sleep(100);

                waitForResponse();

                sendCmd("sys factoryRESET");
                sendCmd("sys get hweui");
                sendCmd("mac get deveui");

                // For TTN
                sendCmd("mac set devaddr AAABBBEE");  // Set own address
                Thread.Sleep(1000);
                sendCmd("mac set appskey 2B7E151628AED2A6ABF7158809CF4F3D");
                Thread.Sleep(1000);

                sendCmd("mac set nwkskey 2B7E151628AED2A6ABF7158809CF4F3D");
                Thread.Sleep(1000);

                sendCmd("mac set adr off");
                Thread.Sleep(1000);

                sendCmd("mac set rx2 3 868400000");//869525000
                Thread.Sleep(1000);

                sendCmd("mac join abp");
                sendCmd("mac get status");
                sendCmd("mac get devaddr");
                Thread.Sleep(1000);
                #endregion
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
                    #region lora
                    var jsonStr = JsonConvert.SerializeObject(sensor);
                    Debug.Print("kirim :" + jsonStr);
                    //PrintToLcd("send count: " + counter);
                    sendData(jsonStr);
                    Thread.Sleep(INTERVAL);
                    byte[] rx_data = new byte[20];

                    if (UART.BytesToRead > 0)
                    {
                        var count = UART.Read(rx_data, 0, rx_data.Length);
                        if (count > 0)
                        {
                            Debug.Print("count:" + count);
                            var hasil = new string(System.Text.Encoding.UTF8.GetChars(rx_data));
                            Debug.Print("read:" + hasil);

                            //mac_rx 2 AABBCC
                        }
                    }
                    #endregion
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

        #region lora
        static void UART_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {

            _dataInLora = UART.Deserialize();
            for (int index = 0; index < _dataInLora.Length; index++)
            {
                rx = _dataInLora[index];
                //if error
                if (_dataInLora[index].Length > 5)
                {

                    //if receive data
                    if (rx.Substring(0, 6) == "mac_rx")
                    {
                        string hex = _dataInLora[index].Substring(9);

                        //update display
                      
                        byte[] data = StringToByteArrayFastest(hex);
                        string decoded = new String(UTF8Encoding.UTF8.GetChars(data));
                        Debug.Print("decoded:" + decoded);

                    }
                }
            }
            Debug.Print(rx);
        }
        public static byte[] StringToByteArrayFastest(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }
        public static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            //return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }
        static void sendCmd(string cmd)
        {
            byte[] rx_data = new byte[20];
            Debug.Print(cmd);
            Debug.Print("\n");
            // flush all data
            //UART.Flush();
            // send some data
            var tx_data = Encoding.UTF8.GetBytes(cmd);
            UART.Write(tx_data, 0, tx_data.Length);
            tx_data = Encoding.UTF8.GetBytes("\r\n");
            UART.Write(tx_data, 0, tx_data.Length);
            Thread.Sleep(100);
            while (!UART.IsOpen)
            {
                UART.Open();
                Thread.Sleep(100);
            }
            if (UART.BytesToRead>0)
            {
                var count = UART.Read(rx_data, 0, rx_data.Length);
                if (count > 0)
                {
                    Debug.Print("count cmd:" + count);
                    var hasil = new string(System.Text.Encoding.UTF8.GetChars(rx_data));
                    Debug.Print("read cmd:" + hasil);
                }
            }
        }
        static void waitForResponse()
        {
            byte[] rx_data = new byte[20];

            while (!UART.IsOpen)
            {
                UART.Open();
                Thread.Sleep(100);
            }
            if (UART.BytesToRead>0)
            {
                var count = UART.Read(rx_data, 0, rx_data.Length);
                if (count > 0)
                {
                    Debug.Print("count res:" + count);
                    var hasil = new string(System.Text.Encoding.UTF8.GetChars(rx_data));
                    Debug.Print("read res:" + hasil);
                }

            }
        }
        public static string Unpack(string input)
        {
            byte[] b = new byte[input.Length / 2];

            for (int i = 0; i < input.Length; i += 2)
            {
                b[i / 2] = (byte)((FromHex(input[i]) << 4) | FromHex(input[i + 1]));
            }
            return new string(Encoding.UTF8.GetChars(b));
        }
        public static int FromHex(char digit)
        {
            if ('0' <= digit && digit <= '9')
            {
                return (int)(digit - '0');
            }

            if ('a' <= digit && digit <= 'f')
                return (int)(digit - 'a' + 10);

            if ('A' <= digit && digit <= 'F')
                return (int)(digit - 'A' + 10);

            throw new ArgumentException("digit");
        }
        static char getHexHi(char ch)
        {
            int nibbleInt = ch >> 4;
            char nibble = (char)nibbleInt;
            int res = (nibble > 9) ? nibble + 'A' - 10 : nibble + '0';
            return (char)res;
        }
        static char getHexLo(char ch)
        {
            int nibbleInt = ch & 0x0f;
            char nibble = (char)nibbleInt;
            int res = (nibble > 9) ? nibble + 'A' - 10 : nibble + '0';
            return (char)res;
        }
        static void sendData(string msg)
        {
            byte[] rx_data = new byte[20];
            char[] data = msg.ToCharArray();
            Debug.Print("mac tx uncnf 1 ");
            var tx_data = Encoding.UTF8.GetBytes("mac tx uncnf 1 ");
            UART.Write(tx_data, 0, tx_data.Length);

            // Write data as hex characters
            foreach (char ptr in data)
            {
                tx_data = Encoding.UTF8.GetBytes(new string(new char[] { getHexHi(ptr) }));
                UART.Write(tx_data, 0, tx_data.Length);
                tx_data = Encoding.UTF8.GetBytes(new string(new char[] { getHexLo(ptr) }));
                UART.Write(tx_data, 0, tx_data.Length);


                Debug.Print(new string(new char[] { getHexHi(ptr) }));
                Debug.Print(new string(new char[] { getHexLo(ptr) }));
            }
            tx_data = Encoding.UTF8.GetBytes("\r\n");
            UART.Write(tx_data, 0, tx_data.Length);
            Debug.Print("\n");
            Thread.Sleep(5000);

            if (UART.BytesToRead > 0)
            {
                var count = UART.Read(rx_data, 0, rx_data.Length);
                if (count > 0)
                {
                    Debug.Print("count after:" + count);
                    var hasil = new string(System.Text.Encoding.UTF8.GetChars(rx_data));
                    Debug.Print("read after:" + hasil);
                }
            }
        }
        #endregion

    }


}

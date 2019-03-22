using BMC.LowLevelDrivers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Gpio;

namespace BMC.Aquascape
{
    class Program
    {
        static void Main(string[] args)
        {

            DS1307 jam = new DS1307();
            var nowDate = DateTime.Now;
            Console.WriteLine("date :"+nowDate);
            jam.SetDateTime(nowDate);
            Console.WriteLine(jam.GetDateTime().ToString());
            ADS1115 analog = new ADS1115();
            var Relay1 = Pi.Gpio.Pin06;
            var Relay2 = Pi.Gpio.Pin13;
            var Limit1 = Pi.Gpio.Pin19;
            var Limit2 = Pi.Gpio.Pin26;
            var Relay1Status = true;
            var Relay2Status = true;

            while (true)
            {
                
                for (int i = 0; i < 4; i++)
                {
                    //Console.WriteLine($"A{i} = " + analog.Read(i));
                    Console.WriteLine($"A{i} = {analog.ReadADC(16, i)}");
                }
                WriteDigital(Relay1, Relay1Status);
                WriteDigital(Relay2, Relay2Status);
                Relay1Status = !Relay1Status;
                Relay2Status = !Relay2Status;

                Console.WriteLine($"Relay 1: {ReadDigital(Relay1)}");
                Console.WriteLine($"Relay 2: {ReadDigital(Relay2)}");
                Console.WriteLine($"Limit 1: {ReadDigital(Limit1)}");
                Console.WriteLine($"Limit 2: {ReadDigital(Limit2)}");
                Thread.Sleep(2000);
            }
        }

        /// <summary>
        /// Write the given value to the given pin.
        /// </summary>
        /// <param name="pin">The pin to set.</param>
        /// <param name="state">The new state of the pin.</param>
        public static void WriteDigital(GpioPin gpioPin, bool state)
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
        public static bool ReadDigital(GpioPin gpioPin)
        {
          

            if (gpioPin.PinMode != GpioPinDriveMode.Input)
                gpioPin.PinMode = GpioPinDriveMode.Input;

            return gpioPin.Read() == true;
        }
    }
}

using BMC.LowLevelDrivers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BMC.Aquascape
{
    class Program
    {
        static void Main(string[] args)
        {
            ADS1115 analog = new ADS1115();
            while (true)
            {
                Console.WriteLine("A0 = "+ analog.ReadADC(16,0));
                Thread.Sleep(500);
            }
        }
    }
}

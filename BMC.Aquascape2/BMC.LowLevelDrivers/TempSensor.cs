using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BMC.LowLevelDrivers
{
    public class TempSensor
    {

        public double Read()
        {
            try
            {
                DirectoryInfo devicesDir = new DirectoryInfo("/sys/bus/w1/devices");

                foreach (var deviceDir in devicesDir.EnumerateDirectories("28*"))
                {
                    var w1slavetext =
                        deviceDir.GetFiles("w1_slave").FirstOrDefault().OpenText().ReadToEnd();
                    string temptext =
                        w1slavetext.Split(new string[] { "t=" }, StringSplitOptions.RemoveEmptyEntries)[1];

                    double temp = double.Parse(temptext) / 1000;

                    Console.WriteLine(string.Format("Device {0} reported temperature {1}C",
                        deviceDir.Name, temp));
                    return temp;
                }
            }
            catch (Exception ex) { Console.WriteLine("read temp error:"+ex); }
            return -1;
        }
    }
}

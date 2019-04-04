using System;

namespace BMC.Aquascape.Model
{
    public class DeviceData
    {
        public bool Relay1 { get; set; }
        public bool Relay2 { get; set; }
        public double Temp { get; set; }
        public double TDS1 { get; set; }
        public double TDS2 { get; set; }
        public bool LimitSwitch1 { get; set; }
        public bool LimitSwitch2 { get; set; }
    }
    public class DeviceAction
    {
        public string ActionName { get; set; }
        public string[] Params { get; set; }
    }

    public class DeviceData2
    {
        public string Relay1 { get; set; }
        public string Relay2 { get; set; }
        public double Temp { get; set; }
        public double TDS1 { get; set; }
        public double TDS2 { get; set; }
        public string LimitSwitch1 { get; set; }
        public string LimitSwitch2 { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}

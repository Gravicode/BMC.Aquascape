﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BMC.LowLevelDrivers
{
    public class ChannelReadingDone : EventArgs
    {
        public int RawValue { get; set; }
        public byte Channel { get; set; }
        public int SlaveAddress { get; internal set; }
    }
}

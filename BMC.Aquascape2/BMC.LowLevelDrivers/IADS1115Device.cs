using System;

namespace BMC.LowLevelDrivers
{
    public interface IADS1115Device
    {
        event EventHandler<ChannelReadingDone> ChannelChanged;
    }
}
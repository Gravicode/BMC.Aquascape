﻿using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using Windows.Devices.I2c;
//using Windows.System.Threading;
//using Iot.Common.Utils;
using static BMC.LowLevelDrivers.ADS1115_Constants;
using System.Timers;
using Mono.Linux.I2C;

namespace BMC.LowLevelDrivers
{
    public class ADS1115Device : IADS1115Device,IDisposable
    {
        public event EventHandler<ChannelReadingDone> ChannelChanged;

        private I2CDevice _ads1115;
        private bool disposed;
        private byte[] read;
        private byte[] write;
        private int[] datas;
        public static byte GetAddress(bool a0, bool a1) => (byte)(0x48 | (a0 ? 1 : 0) | (a1 ? 2 : 0));

        //public void Dispose() => this.Dispose(true);

  

        //private readonly I2cDevice _ads1115;
        private readonly byte _channelsToReport;
        private Timer _ads1115Timer;

        public ADS1115Device()
        {
            datas = new int[4];
            for(int i = 0; i < 4; i++)
            {
                datas[i] = 0;
            }
            //_channelsToReport = channelsToReport;
            I2CBus i2cBus = new Mono.Linux.I2C.I2CBus(0x01);

            this._ads1115 = new Mono.Linux.I2C.I2CDevice(i2cBus, GetAddress(false, false));

            this.disposed = false;
            this.read = new byte[1];
            this.write = new byte[1];
        }

        public void Dispose()
        {
            //_ads1115.Dispose();
            _ads1115Timer = null;
        }

        public void Start()
        {
            _ads1115Timer = new Timer(250);
            _ads1115Timer.Elapsed += ads1115_tick;
            _ads1115Timer.Start();
            //ThreadPoolTimer.CreatePeriodicTimer(ads1115_tick, TimeSpan.FromMilliseconds(250));
        }

      

        private void ads1115_tick(object sender, ElapsedEventArgs e)
        {
            StartReading();
        }

        private void StartReading()
        {
            for (byte channel = 0; channel < 3; channel++)
            {
                //if (_channelsToReport.FlagIsTrue(channel, false))
                {
                    ReadChannel(channel);
                }
            }
        }

        public int ReadRaw(int channel)
        {
            if (channel >= 0 && channel < datas.Length)
            {
                return datas[channel];
            }
            return -1;
        }
        private void ReadChannel(byte channel)
        {
            var reading = readADC_SingleEnded(channel);
            datas[channel] = reading;
            //_ads1115.ConnectionSettings.SlaveAddress
            ChannelChanged?.Invoke(this, new ChannelReadingDone {RawValue = reading, Channel = channel, SlaveAddress = 0 });
        }

        private int readADC_SingleEnded(byte channel)
        {
            if (channel > 3)
            {
                return 0;
            }

            var config = Set_Defaults();

            // Set single-ended input channel
            switch (channel)
            {
                case (0):
                    config |= (ushort) ADS1115_REG_CONFIG_MUX_SINGLE_0.GetHashCode();
                    break;
                case (1):
                    config |= (ushort) ADS1115_REG_CONFIG_MUX_SINGLE_1.GetHashCode();
                    break;
                case (2):
                    config |= (ushort) ADS1115_REG_CONFIG_MUX_SINGLE_2.GetHashCode();
                    break;
                case (3):
                    config |= (ushort) ADS1115_REG_CONFIG_MUX_SINGLE_3.GetHashCode();
                    break;
            }

            // Set 'start single-conversion' bit
            config |= (ushort) ADS1115_REG_CONFIG_OS_SINGLE.GetHashCode();

            return GetReadingFromConverter(config);
        }

        private static ushort Set_Defaults()
        {
            // Start with default values
            var config = (ushort) (ADS1115_REG_CONFIG_CQUE_NONE.GetHashCode() |  // Disable the comparator (default val)
                                ADS1115_REG_CONFIG_CLAT_NONLAT.GetHashCode() |   // Non-latching (default val)
                                ADS1115_REG_CONFIG_CPOL_ACTVLOW.GetHashCode() |  // Alert/Rdy active low   (default val)
                                ADS1115_REG_CONFIG_CMODE_TRAD.GetHashCode() |    // Traditional comparator (default val)
                                ADS1115_REG_CONFIG_DR_128SPS.GetHashCode() |    // 128 samples per second
                                ADS1115_REG_CONFIG_MODE_SINGLE.GetHashCode());   // Single-shot mode (default)

            // Set PGA/voltage range
            //config |= (ushort)GetConstantAsByte("ADS1115_REG_CONFIG_PGA_6_144V"); // +/- 6.144V range (limited to VDD +0.3V max!)
            //config |=(byte)ADS1115_REG_CONFIG_PGA_1_024V.GetHashCode();
            config |= (ushort)ADS1115_REG_CONFIG_PGA_6_144V.GetHashCode();
            return config;
        }

        private int GetReadingFromConverter(ushort config)
        {
            // Write config register to the ADC
            var pointerCommand = (new[] {(byte) ADS1015_REG_POINTER_CONFIG.GetHashCode()}).Union(BitConverter.GetBytes(config)).ToArray();
            _ads1115.Write((byte)ADS1015_REG_POINTER_CONFIG, BitConverter.GetBytes(config));
            //_ads1115.Write(pointerCommand);

            var dataBuffer = new byte[2];

            Task.Delay(TimeSpan.FromMilliseconds(ADS1115_CONVERSIONDELAY.GetHashCode())).Wait();

            //pointerCommand = new[] { (byte)ADS1015_REG_POINTER_CONVERT.GetHashCode() };
            _ads1115.ReadBytes((byte)ADS1015_REG_POINTER_CONVERT, (byte)dataBuffer.Length, dataBuffer);
            //_ads1115.WriteRead(pointerCommand, dataBuffer);

            // Read the conversion results
            var rawReading = dataBuffer[0] << 8 | dataBuffer[1];
            return rawReading;
        }
    }
}
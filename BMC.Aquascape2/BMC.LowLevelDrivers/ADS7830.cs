using Mono.Linux.I2C;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BMC.LowLevelDrivers
{
    public class ADS7830
    {
        private I2CDevice device;
        private bool disposed;
        private byte[] read;
        private byte[] write;

        public static byte GetAddress(bool a0, bool a1) => (byte)(0x48 | (a0 ? 1 : 0) | (a1 ? 2 : 0));

        public void Dispose() => this.Dispose(true);

        public ADS7830()
        {
            var i2cBus = new Mono.Linux.I2C.I2CBus(0x01);
            this.device = new Mono.Linux.I2C.I2CDevice(i2cBus, ADS7830.GetAddress(false, false));
            this.disposed = false;
            this.read = new byte[1];
            this.write = new byte[1];
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    //this.device.Dispose();
                }

                this.disposed = true;
            }
        }

        public int ReadRaw(int channel)
        {
            if (this.disposed) throw new ObjectDisposedException(nameof(ADS7830));
            if (channel > 8 || channel < 0) throw new ArgumentOutOfRangeException(nameof(channel));

            this.write[0] = (byte)(0x84 | ((channel % 2 == 0 ? channel / 2 : (channel - 1) / 2 + 4) << 4));

            this.read[0] = this.device.ReadByte(this.write[0]);

            return this.read[0];
        }

        public double Read(int channel) => this.ReadRaw(channel) / 255.0;
    }
}

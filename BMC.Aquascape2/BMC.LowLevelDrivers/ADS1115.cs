using Mono.Linux.I2C;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BMC.LowLevelDrivers
{
    public class ADS1115
    {
        private I2CDevice device;
        private bool disposed;
        private byte[] read;
        private byte[] write;

        public static byte GetAddress(bool a0, bool a1) => (byte)(0x48 | (a0 ? 1 : 0) | (a1 ? 2 : 0));

        public void Dispose() => this.Dispose(true);

        public ADS1115()
        {
            I2CBus i2cBus = new Mono.Linux.I2C.I2CBus(0x01);

            this.device = new Mono.Linux.I2C.I2CDevice(i2cBus, GetAddress(false, false));
           
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
            if (this.disposed) throw new ObjectDisposedException(nameof(ADS1115));
            if (channel > 8 || channel < 0) throw new ArgumentOutOfRangeException(nameof(channel));

            this.write[0] = (byte)(0x84 | ((channel % 2 == 0 ? channel / 2 : (channel - 1) / 2 + 4) << 4));

            this.read[0] = this.device.ReadByte(this.write[0]);

            return this.read[0];
        }
        
        /// <summary>
        /// Reads the specified ADC channel and returns value
        /// </summary>
        /// <param name="inputNumber"></param>
        /// <returns></returns>
        public string ReadADC(int resolution, int inputNumber)
        {
            try
            {


                // Build transaction bytes
                // Config byte 1
                byte config1 = (byte)(192 + (16 * inputNumber) + (2 * resolution) + 1);

                // Config byte 2
                byte config2 = 131;

                // create write buffer (we need three bytes)
                byte[] registerADS1115 = new byte[3] { 1, config1, config2 };
                this.device.WriteBytes(registerADS1115[0], 2, new byte[] { registerADS1115[1], registerADS1115[2] });
                // Create write transaction
                //xAction[0] = I2CDevice.CreateWriteTransaction(registerADS1115);
                // excecute ADS1115 setup
                //myI2C.Execute(xAction, 1000);

                // Wait for conversion
                Thread.Sleep(15);

                // Set to conversion register
                registerADS1115 = new byte[1] { 0 };
                //this.device.Write(registerADS1115[0],);
                //xAction[0] = I2CDevice.CreateWriteTransaction(registerADS1115);
                // Execute set to conversion
                //myI2C.Execute(xAction, 1000);

                // create read buffer
                byte[] readADS1115 = new byte[2];
                this.device.ReadBytes(0, 2, readADS1115);
                // Read ADC values
                //xAction[0] = I2CDevice.CreateReadTransaction(readADS1115);
                //myI2C.Execute(xAction, 1000);

                // return values
                return (readADS1115[0] * 256 + readADS1115[1]).ToString();
            }
            catch (Exception exp)
            {
                return exp.Message;
            }
        }

        public double Read(int channel) => this.ReadRaw(channel) / 255.0;
    }
}

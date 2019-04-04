using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using Mono.Linux.I2C;
using System;
using System.Threading;

namespace BMC.LowLevelDrivers
{
    public  class ADS1x15
    {

        const int ADS1x15_DEFAULT_ADDRESS = 0x48;
        const int ADS1x15_POINTER_CONVERSION = 0x00;
        const int ADS1x15_POINTER_CONFIG = 0x01;
        const int ADS1x15_POINTER_LOW_THRESHOLD = 0x02;
        const int ADS1x15_POINTER_HIGH_THRESHOLD = 0x03;
        const int ADS1x15_CONFIG_OS_SINGLE = 0x8000;
        const int ADS1x15_CONFIG_MUX_OFFSET = 12;

        public static Dictionary<int, int> ADS1x15_CONFIG_GAIN = new Dictionary<int, int> {
        {
            2 / 3,
            0x0000},
        {
            1,
            0x0200},
        {
            2,
            0x0400},
        {
            4,
            0x0600},
        {
            8,
            0x0800},
        {
            16,
            0x0A00}};

        const int ADS1x15_CONFIG_MODE_CONTINUOUS = 0;
        
        const int ADS1x15_CONFIG_MODE_SINGLE = 256;

        public static Dictionary<int, int> ADS1015_CONFIG_DR = new Dictionary<int, int> {
        {
            128,
            0},
        {
            250,
            32},
        {
            490,
            64},
        {
            920,
            96},
        {
            1600,
            128},
        {
            2400,
            160},
        {
            3300,
            192}};

        public static Dictionary<int, int> ADS1115_CONFIG_DR = new Dictionary<int, int> {
        {
            8,
            0},
        {
            16,
            32},
        {
            32,
            64},
        {
            64,
            96},
        {
            128,
            128},
        {
            250,
            160},
        {
            475,
            192},
        {
            860,
            224}};

        const int ADS1x15_CONFIG_COMP_WINDOW = 16;

        const int ADS1x15_CONFIG_COMP_ACTIVE_HIGH = 8;

        const int ADS1x15_CONFIG_COMP_LATCHING = 4;

        public static Dictionary<int, int> ADS1x15_CONFIG_COMP_QUE = new Dictionary<int, int> {
        {
            1,
            0},
        {
            2,
            1},
        {
            4,
            2}};

        public static int ADS1x15_CONFIG_COMP_QUE_DISABLE = 3;
        public static byte GetAddress(bool a0, bool a1) => (byte)(0x48 | (a0 ? 1 : 0) | (a1 ? 2 : 0));
        // Base functionality for ADS1x15 analog to digital converters.
        private I2CDevice _device;

        public ADS1x15(int address = ADS1x15_DEFAULT_ADDRESS, object i2c = null)
        {
            I2CBus i2cBus = new Mono.Linux.I2C.I2CBus(0x01);
            this._device = new Mono.Linux.I2C.I2CDevice(i2cBus, GetAddress(false, false));
           
        }

        // Retrieve the default data rate for this ADC (in samples per second).
        //         Should be implemented by subclasses.
        //         
        public virtual int _data_rate_default()
        {
            throw new Exception("Subclasses must implement _data_rate_default!");
        }

        // Subclasses should override this function and return a 16-bit value
        //         that can be OR'ed with the config register to set the specified
        //         data rate.  If a value of None is specified then a default data_rate
        //         setting should be returned.  If an invalid or unsupported data_rate is
        //         provided then an exception should be thrown.
        //         
        public virtual int _data_rate_config(int data_rate)
        {
            throw new Exception("Subclass must implement _data_rate_config function!");
        }

        // Subclasses should override this function that takes the low and high
        //         byte of a conversion result and returns a signed integer value.
        //         
        public virtual int _conversion_value(int low, int high)
        {
            throw new Exception("Subclass must implement _conversion_value function!");
        }

        // Perform an ADC read with the provided mux, gain, data_rate, and mode
        //         values.  Returns the signed integer result of the read.
        //         
        public virtual int _read(int mux, int gain, int data_rate, int mode)
        {
            var config = ADS1x15_CONFIG_OS_SINGLE;
            // Specify mux value.
            config |= (mux & 7) << ADS1x15_CONFIG_MUX_OFFSET;
            // Validate the passed in gain and then set it in the config.
            if (!ADS1x15_CONFIG_GAIN.ContainsKey(gain))
            {
                throw new Exception("Gain must be one of: 2/3, 1, 2, 4, 8, 16");
            }
            config |= ADS1x15_CONFIG_GAIN[gain];
            // Set the mode (continuous or single shot).
            config |= mode;
            // Get the default data rate if none is specified (default differs between
            // ADS1015 and ADS1115).
            if (data_rate <= 0)
            {
                data_rate = this._data_rate_default();
            }
            // Set the data rate (this is controlled by the subclass as it differs
            // between ADS1015 and ADS1115).
            config |= this._data_rate_config(data_rate);
            config |= ADS1x15_CONFIG_COMP_QUE_DISABLE;
            // Send the config value to start the ADC conversion.
            // Explicitly break the 16-bit value down to a big endian pair of bytes.
            this._device.Write((byte)ADS1x15_POINTER_CONFIG, new byte[]{
                (byte)(config >> 8 & 255),(byte)(
                config & 255) });
            // Wait for the ADC sample to finish based on the sample rate plus a
            // small offset to be sure (0.1 millisecond).
            Thread.Sleep(100);// 1.0 / data_rate + 0.0001);
            // Retrieve the result.
            var result = this._device.Read((byte)ADS1x15_POINTER_CONVERSION, 2);
            return this._conversion_value(result[1], result[0]);
        }

        // Perform an ADC read with the provided mux, gain, data_rate, and mode
        //         values and with the comparator enabled as specified.  Returns the signed
        //         integer result of the read.
        //         
        public virtual object _read_comparator(
            int mux,
            int gain,
            int data_rate,
            int mode,
            int high_threshold,
            int low_threshold,
            bool active_low,
            bool traditional,
            bool latching,
            int num_readings)
        {
            if(!(num_readings == 1 || num_readings == 2 || num_readings == 4))
             throw new Exception("Num readings must be 1, 2, or 4!");
            // Set high and low threshold register values.
            this._device.Write(ADS1x15_POINTER_HIGH_THRESHOLD, new byte[] {
                (byte)(high_threshold >> 8 & 255),(byte)(
                high_threshold & 255)});
            this._device.Write(ADS1x15_POINTER_LOW_THRESHOLD, new byte[] {
                (byte)(low_threshold >> 8 & 255),
                (byte)(low_threshold & 255)
            });
            // Now build up the appropriate config register value.
            var config = ADS1x15_CONFIG_OS_SINGLE;
            // Specify mux value.
            config |= (mux & 7) << ADS1x15_CONFIG_MUX_OFFSET;
            // Validate the passed in gain and then set it in the config.
            if (!ADS1x15_CONFIG_GAIN.ContainsKey(gain))
            {
                throw new Exception("Gain must be one of: 2/3, 1, 2, 4, 8, 16");
            }
            config |= ADS1x15_CONFIG_GAIN[gain];
            // Set the mode (continuous or single shot).
            config |= mode;
            // Get the default data rate if none is specified (default differs between
            // ADS1015 and ADS1115).
            if (data_rate <= 0)
            {
                data_rate = this._data_rate_default();
            }
            // Set the data rate (this is controlled by the subclass as it differs
            // between ADS1015 and ADS1115).
            config |= this._data_rate_config(data_rate);
            // Enable window mode if required.
            if (!traditional)
            {
                config |= ADS1x15_CONFIG_COMP_WINDOW;
            }
            // Enable active high mode if required.
            if (!active_low)
            {
                config |= ADS1x15_CONFIG_COMP_ACTIVE_HIGH;
            }
            // Enable latching mode if required.
            if (latching)
            {
                config |= ADS1x15_CONFIG_COMP_LATCHING;
            }
            // Set number of comparator hits before alerting.
            config |= ADS1x15_CONFIG_COMP_QUE[num_readings];
            // Send the config value to start the ADC conversion.
            // Explicitly break the 16-bit value down to a big endian pair of bytes.
            this._device.Write(ADS1x15_POINTER_CONFIG, new byte[] {
                (byte)(config >> 8 & 255),
                (byte)(config & 255)
            });
            // Wait for the ADC sample to finish based on the sample rate plus a
            // small offset to be sure (0.1 millisecond).
            Thread.Sleep(100);// 1.0 / data_rate + 0.0001);
            // Retrieve the result.
            var result = this._device.Read(ADS1x15_POINTER_CONVERSION, 2);
            return this._conversion_value(result[1], result[0]);
        }

        // Read a single ADC channel and return the ADC value as a signed integer
        //         result.  Channel must be a value within 0-3.
        //         
        public virtual int read_adc(int channel, int gain = 1, int data_rate = -1)
        {
            if(channel<0 || channel > 3)
                throw new Exception("Channel must be a value within 0-3!");
            // Perform a single shot read and set the mux value to the channel plus
            // the highest bit (bit 3) set.
            return this._read(channel + 4, gain, data_rate, ADS1x15_CONFIG_MODE_SINGLE);
        }

        // Read the difference between two ADC channels and return the ADC value
        //         as a signed integer result.  Differential must be one of:
        //           - 0 = Channel 0 minus channel 1
        //           - 1 = Channel 0 minus channel 3
        //           - 2 = Channel 1 minus channel 3
        //           - 3 = Channel 2 minus channel 3
        //         
        public virtual object read_adc_difference(int differential, int gain = 1, int data_rate = -1)
        {
            if(differential <0 || differential > 3)
                throw new Exception("Differential must be a value within 0-3!");
            // Perform a single shot read using the provided differential value
            // as the mux value (which will enable differential mode).
            return this._read(differential, gain, data_rate, ADS1x15_CONFIG_MODE_SINGLE);
        }

        // Start continuous ADC conversions on the specified channel (0-3). Will
        //         return an initial conversion result, then call the get_last_result()
        //         function to read the most recent conversion result. Call stop_adc() to
        //         stop conversions.
        //         
        public virtual int start_adc(int channel, int gain = 1, int data_rate = -1)
        {
            if(channel <0 || channel > 3)
                throw new Exception("Channel must be a value within 0-3!");
            // Start continuous reads and set the mux value to the channel plus
            // the highest bit (bit 3) set.
            return this._read(channel + 4, gain, data_rate, ADS1x15_CONFIG_MODE_CONTINUOUS);
        }

        // Start continuous ADC conversions between two ADC channels. Differential
        //         must be one of:
        //           - 0 = Channel 0 minus channel 1
        //           - 1 = Channel 0 minus channel 3
        //           - 2 = Channel 1 minus channel 3
        //           - 3 = Channel 2 minus channel 3
        //         Will return an initial conversion result, then call the get_last_result()
        //         function continuously to read the most recent conversion result.  Call
        //         stop_adc() to stop conversions.
        //         
        public virtual int start_adc_difference(int differential, int gain = 1, int data_rate = -1)
        {
            if(differential < 0 || differential >3)
                throw new Exception("Differential must be a value within 0-3!");
            // Perform a single shot read using the provided differential value
            // as the mux value (which will enable differential mode).
            return this._read(differential, gain, data_rate, ADS1x15_CONFIG_MODE_CONTINUOUS);
        }

        // Start continuous ADC conversions on the specified channel (0-3) with
        //         the comparator enabled.  When enabled the comparator to will check if
        //         the ADC value is within the high_threshold & low_threshold value (both
        //         should be signed 16-bit integers) and trigger the ALERT pin.  The
        //         behavior can be controlled by the following parameters:
        //           - active_low: Boolean that indicates if ALERT is pulled low or high
        //                         when active/triggered.  Default is true, active low.
        //           - traditional: Boolean that indicates if the comparator is in traditional
        //                          mode where it fires when the value is within the threshold,
        //                          or in window mode where it fires when the value is _outside_
        //                          the threshold range.  Default is true, traditional mode.
        //           - latching: Boolean that indicates if the alert should be held until
        //                       get_last_result() is called to read the value and clear
        //                       the alert.  Default is false, non-latching.
        //           - num_readings: The number of readings that match the comparator before
        //                           triggering the alert.  Can be 1, 2, or 4.  Default is 1.
        //         Will return an initial conversion result, then call the get_last_result()
        //         function continuously to read the most recent conversion result.  Call
        //         stop_adc() to stop conversions.
        //         
        public virtual object start_adc_comparator(
            int channel,
            int high_threshold,
            int low_threshold,
            int gain = 1,
            int data_rate = -1,
            bool active_low = true,
            bool traditional = true,
            bool latching = false,
            int num_readings = 1)
        {
            if(channel < 0 || channel > 3)
            throw new Exception("Channel must be a value within 0-3!");
            // Start continuous reads with comparator and set the mux value to the
            // channel plus the highest bit (bit 3) set.
            return this._read_comparator(channel + 4, gain, data_rate, ADS1x15_CONFIG_MODE_CONTINUOUS, high_threshold, low_threshold, active_low, traditional, latching, num_readings);
        }

        // Start continuous ADC conversions between two channels with
        //         the comparator enabled.  See start_adc_difference for valid differential
        //         parameter values and their meaning.  When enabled the comparator to will
        //         check if the ADC value is within the high_threshold & low_threshold value
        //         (both should be signed 16-bit integers) and trigger the ALERT pin.  The
        //         behavior can be controlled by the following parameters:
        //           - active_low: Boolean that indicates if ALERT is pulled low or high
        //                         when active/triggered.  Default is true, active low.
        //           - traditional: Boolean that indicates if the comparator is in traditional
        //                          mode where it fires when the value is within the threshold,
        //                          or in window mode where it fires when the value is _outside_
        //                          the threshold range.  Default is true, traditional mode.
        //           - latching: Boolean that indicates if the alert should be held until
        //                       get_last_result() is called to read the value and clear
        //                       the alert.  Default is false, non-latching.
        //           - num_readings: The number of readings that match the comparator before
        //                           triggering the alert.  Can be 1, 2, or 4.  Default is 1.
        //         Will return an initial conversion result, then call the get_last_result()
        //         function continuously to read the most recent conversion result.  Call
        //         stop_adc() to stop conversions.
        //         
        public virtual object start_adc_difference_comparator(
            int differential,
            int high_threshold,
            int low_threshold,
            int gain = 1,
            int data_rate = -1,
            bool active_low = true,
            bool traditional = true,
            bool latching = false,
            int num_readings = 1)
        {
            if(differential<0 || differential > 3)
             throw new Exception("Differential must be a value within 0-3!");
            // Start continuous reads with comparator and set the mux value to the
            // channel plus the highest bit (bit 3) set.
            return this._read_comparator(differential, gain, data_rate, ADS1x15_CONFIG_MODE_CONTINUOUS, high_threshold, low_threshold, active_low, traditional, latching, num_readings);
        }

        // Stop all continuous ADC conversions (either normal or difference mode).
        //         
        public virtual void stop_adc()
        {
            // Set the config register to its default value of 0x8583 to stop
            // continuous conversions.
            var config = 34179;
            this._device.Write(ADS1x15_POINTER_CONFIG, new byte[] {
                (byte)(config >> 8 & 255),
                (byte)(config & 255)
            });
        }

        // Read the last conversion result when in continuous conversion mode.
        //         Will return a signed integer value.
        //         
        public virtual int get_last_result()
        {
            // Retrieve the conversion register value, convert to a signed int, and
            // return it.
            var result = this._device.Read(ADS1x15_POINTER_CONVERSION, 2);
            return this._conversion_value(result[1], result[0]);
        }
    }


        // ADS1115 16-bit analog to digital converter instance.
        public class ADS1115_PY
            : ADS1x15
        {

            public ADS1115_PY()
                
            {
            }

            public override int _data_rate_default()
            {
                // Default from datasheet page 16, config register DR bit default.
                return 128;
            }

            public override int _data_rate_config(int data_rate)
            {
                if (!ADS1115_CONFIG_DR.ContainsKey(data_rate))
                {
                    throw new Exception("Data rate must be one of: 8, 16, 32, 64, 128, 250, 475, 860");
                }
                return ADS1115_CONFIG_DR[data_rate];
            }

            public override int _conversion_value(int low, int high)
            {
                // Convert to 16-bit signed value.
                var value = (high & 255) << 8 | low & 255;
                // Check for sign bit and turn into a negative value if set.
                if ((value & 32768) != 0)
                {
                    value -= 1 << 16;
                }
                return value;
            }
        }

        // ADS1015 12-bit analog to digital converter instance.
        public class ADS1015
            : ADS1x15
        {

            public ADS1015()
            {
            }

            public override int _data_rate_default()
            {
                // Default from datasheet page 19, config register DR bit default.
                return 1600;
            }

            public override int _data_rate_config(int data_rate)
            {
                if (!ADS1015_CONFIG_DR.ContainsKey(data_rate))
                {
                    throw new Exception("Data rate must be one of: 128, 250, 490, 920, 1600, 2400, 3300");
                }
                return ADS1015_CONFIG_DR[data_rate];
            }

            public override int _conversion_value(int low, int high)
            {
                // Convert to 12-bit signed value.
                var value = (high & 255) << 4 | (low & 255) >> 4;
                // Check for sign bit and turn into a negative value if set.
                if ((value & 2048) != 0)
                {
                    value -= 1 << 12;
                }
                return value;
            }
        }
    
}
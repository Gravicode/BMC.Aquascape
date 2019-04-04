using BMC.Aquascape.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BMC.AquascapeGateway
{
    class Program
    {
        private static HttpClient _client;

        public static HttpClient client
        {
            get
            {
                if (_client == null) _client = new HttpClient();
                return _client;
            }

        }
        static void Main(string[] args)
        {
            Console.WriteLine("start mqtt");
            MqttService mqtt = new MqttService();
            Console.WriteLine("gateway is ready, waiting for data...");
            mqtt.DataReceived += (string Message) =>
            {
                try
                {
                    SendToPowerBI(JsonConvert.DeserializeObject<DeviceData>(Message));
                }
                catch(Exception ex)
                {
                    Console.WriteLine("ERROR:"+ex);
                }
            };
            var INTERVAL = int.Parse(ConfigurationManager.AppSettings["Interval"]);
            Thread.Sleep(Timeout.Infinite);
        }

      

        static async void SendToPowerBI(DeviceData data)
        {
            //"https://api.powerbi.com/beta/e4a5cd36-e58f-4f98-8a1a-7a8e545fc65a/datasets/fdd35f45-854c-48b1-847b-1d0db62076cf/rows?key=C%2FnPcyGr4xDAHDmhgtX1AHEPtrU225NnQExPv%2FBOvPQowBDXQ674MFahutRyCpo0LZmo3BZerFvQE6M8UJ46XA%3D%3D"
            var url = ConfigurationManager.AppSettings["PowerBiUrl"];
            //SensorData2 data2 = new SensorData2() { Ph = data.Ph, Tds1 = data.Tds1, Tds2 = data.Tds2, Temp1 = data.Temp1, Temp2 = data.Temp2, Temp3 = data.Temp3, WaterDist = data.WaterDist };
            var data2 = new DeviceData2() { LimitSwitch1 = data.LimitSwitch1.ToString(), LimitSwitch2=data.LimitSwitch2.ToString(), Relay1=data.Relay1.ToString(), Relay2=data.Relay2.ToString(), TDS1=data.TDS1, TDS2 = data.TDS2, Temp=data.Temp, TimeStamp=DateTime.Now };
            var jsonSettings = new JsonSerializerSettings();
            jsonSettings.DateFormatString = "yyyy-MM-ddThh:mm:ss.fffZ";
            Console.WriteLine(JsonConvert.SerializeObject(data2, jsonSettings));
            var content = new StringContent(JsonConvert.SerializeObject(data2, jsonSettings), Encoding.UTF8, "application/json");
            var res = await client.PostAsync(url, content, CancellationToken.None);
            if (res.IsSuccessStatusCode)
            {
                Console.WriteLine("data sent to power bi - " + DateTime.Now);
            }
            else
            {
                Console.WriteLine("Fail to send to Power BI");
            }
        }
    }
}

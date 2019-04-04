using BMC.Aquascape.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace BMC.Aquascape
{
    public class MqttService
    {
        public delegate void CommandReceivedHandler(string Message);
        public event CommandReceivedHandler CommandReceived;
        public MqttService()
        {
            SetupMqtt();
        }
        MqttClient MqttClient;
        const string DataTopic = "bmc/aquaponic/data";
        const string ControlTopic = "bmc/aquaponic/control";
        public void PublishMessage(string Message)
        {
            MqttClient.Publish(DataTopic, Encoding.UTF8.GetBytes(Message), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
            Console.WriteLine("data published.");
        }
        public void SendCommand(string Message)
        {
            MqttClient.Publish(ControlTopic, Encoding.UTF8.GetBytes(Message), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
        }
        void SetupMqtt()
        {
            string IPBrokerAddress = ConfigurationManager.AppSettings["MqttHost"];
            string ClientUser = ConfigurationManager.AppSettings["MqttUser"];
            string ClientPass = ConfigurationManager.AppSettings["MqttPass"];


            MqttClient = new MqttClient(IPBrokerAddress);
          
            // register a callback-function (we have to implement, see below) which is called by the library when a message was received
            MqttClient.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
            
            // use a unique id as client id, each time we start the application
            var clientId = "bmc-aquaponic";//Guid.NewGuid().ToString();

            MqttClient.Connect(clientId, ClientUser, ClientPass);
            MqttClient.Subscribe(new string[] { DataTopic, ControlTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
            Console.WriteLine("MQTT is connected");
        } // this code runs when a message was received
        void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string ReceivedMessage = Encoding.UTF8.GetString(e.Message);
            if (e.Topic == ControlTopic)
            {
                CommandReceived?.Invoke(ReceivedMessage);
                Console.WriteLine($"{DateTime.Now} - Command Received, [{e.Topic}] : {ReceivedMessage}");
            }
            else
            {
                Console.WriteLine($"{DateTime.Now} - Data Received, [{e.Topic}] : {ReceivedMessage}");
            }
        }
        // Invoke the direct method on the device, passing the payload
        public Task InvokeMethod(string DeviceId, string ActionName = "PlaySound", params string[] Params)
        {
            Console.WriteLine($"Do Action -> {ActionName}");
            return Task.Factory.StartNew(() => {
                var action = new DeviceAction() { ActionName = ActionName, Params = Params };

                SendCommand(JsonConvert.SerializeObject(action));
            });

            //Console.WriteLine("Response status: {0}, payload:", response.Status);
            //Console.WriteLine(response.GetPayloadAsJson());
        }
        

    }
   
}

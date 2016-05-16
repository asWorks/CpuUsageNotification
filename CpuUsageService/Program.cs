using System;
using System.Diagnostics;
using System.Threading;
using Eneter.Messaging.DataProcessing.Serializing;
using Eneter.Messaging.MessagingSystems.MessagingSystemBase;
using Eneter.Messaging.MessagingSystems.WebSocketMessagingSystem;
using Eneter.Messaging.Nodes.Broker;

namespace CpuUsageService
{
    // Message that will be notified.
    public class CpuUpdateMessage
    {
        public float Usage { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // JavaScript uses JSON serializer so set using JSON.
            ISerializer aSerializer = new DataContractJsonStringSerializer();

            // Create broker.
            IDuplexBrokerFactory aBrokerFactory = new DuplexBrokerFactory();
            IDuplexBroker aBroker = aBrokerFactory.CreateBroker();

            // Communicate using WebSockets.
            IMessagingSystemFactory aMessaging = new WebSocketMessagingSystemFactory();
            IDuplexInputChannel anInputChannel = aMessaging.CreateDuplexInputChannel("ws://127.0.0.1:8843/CpuUsage/");

            anInputChannel.ResponseReceiverConnected += (x, y) =>
            {
                Console.WriteLine("Connected client: " + y.ResponseReceiverId);
            };
            anInputChannel.ResponseReceiverDisconnected += (x, y) =>
            {
                Console.WriteLine("Disconnected client: " + y.ResponseReceiverId);
            };

            // Attach input channel and start listeing.
            aBroker.AttachDuplexInputChannel(anInputChannel);

            // Start working thread monitoring the CPU usage.
            bool aStopWorkingThreadFlag = false;
            Thread aWorkingThread = new Thread(() =>
                {
                    PerformanceCounter aCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

                    while (!aStopWorkingThreadFlag)
                    {
                        CpuUpdateMessage aMessage = new CpuUpdateMessage();
                        aMessage.Usage = aCpuCounter.NextValue();

                        //Console.WriteLine(aMessage.Usage);

                        // Serialize the message.
                        object aSerializedMessage = aSerializer.Serialize<CpuUpdateMessage>(aMessage);

                        // Notify subscribers via the broker.
                        // Note: The broker will forward the message to subscribed clients.
                        aBroker.SendMessage("MyCpuUpdate", aSerializedMessage);

                        Thread.Sleep(500);
                    }
                });
            aWorkingThread.Start();

            Console.WriteLine("CpuUsageService is running press ENTER to stop.");
            Console.ReadLine();

            // Wait until the working thread stops.
            aStopWorkingThreadFlag = true;
            aWorkingThread.Join(3000);

            // Detach the input channel and stop listening.
            aBroker.DetachDuplexInputChannel();
        }
    }
}

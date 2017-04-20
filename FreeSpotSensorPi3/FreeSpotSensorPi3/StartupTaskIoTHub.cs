using System;
using Microsoft.Azure.Devices.Client;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using ppatierno.AzureSBLite.Messaging;
using Newtonsoft.Json;
using Windows.System.Threading;
using Windows.Devices.Gpio;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using FreeSpotSensorPi3.StateClasses;
//using System.Net.NetworkInformation;
using Windows.Networking.Connectivity;
using Windows.Networking;



// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace FreeSpotSensorPi3
{
    public sealed class StartupTask : IBackgroundTask
    {
        // Settings for application (prepared for persistance)
        FreeSpotsSettings settings = new FreeSpotsSettings(FreeSpotsSettings.mode.DEBUG2);

        // Current state of application
        FreeSpotsState state = new FreeSpotsState();

        // Message for serialization
        MsgBody msg = new MsgBody();

        // Needed to make sure the application keeps running in the background
        private BackgroundTaskDeferral _backgroundTaskDeferral;

        // IoT hub access added by Tone
        static DeviceClient deviceClient;
        static string iotHubUri = "ObokningsbaraRum.azure-devices.net";
        static string deviceName = "IoTTest";
        static string deviceKey = "W97Sx7FT1ZsgT7+a2EpONkenFVMc+Sxx7PlI412v0hg=";

        // Event hub access
        // mk@acando.com, sender connection information:
        //      Endpoint =sb://ehntesthubns.servicebus.windows.net/;SharedAccessKeyName=ehnTestHubns_send;SharedAccessKey=u0UlsbZGMcPNEemxDrD94B+72iD3keCa2s6xHbknOxk=
        //      Queue path: ehntesthub
        // mikael.koniakowski@fora.se, Sender connection information
        //      Endpoint=sb://freespots4rswer44a-ns.servicebus.windows.net/;SharedAccessKeyName=freespots_send;SharedAccessKey=62hQHXNN8P4nq3trUdQmQ3NeVZLq18gESpsjuQE2Quo=
        //      Queue path: freespots4rswer44a

        //*TONE*private readonly EventHubClient _eventHub = EventHubClient.CreateFromConnectionString("Endpoint=sb://obokbararumsuite.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=npa+nAtVO4aqxtgnFe52XsY+/xss/iHwLZCd9yneOsg=", "obokbararumevent");
        //*TONE*private readonly EventHubClient _eventHub = EventHubClient.CreateFromConnectionString("Endpoint=sb://freespots4rswer44a-ns.servicebus.windows.net/;SharedAccessKeyName=freespots_send;SharedAccessKey=62hQHXNN8P4nq3trUdQmQ3NeVZLq18gESpsjuQE2Quo=", "freespots4rswer44a");
        //private readonly EventHubClient _eventHub = EventHubClient.CreateFromConnectionString("Endpoint=sb://ehntesthubns.servicebus.windows.net/;SharedAccessKeyName=ehnTestHubns_send;SharedAccessKey=u0UlsbZGMcPNEemxDrD94B+72iD3keCa2s6xHbknOxk=", "ehntesthub");

        // Define Pi3 GPIO Pins
        //private const int ledGreenPin = 13;      // Out
        //private const int ledRedPin = 5;        // Out
        //private const int pirPin = 6;           // In

        private const int ledGreenPin = 16; //** TONE* lägg till diod på passande pin.
        private const int ledRedPin = 20; //ledPin from confroomtracker
        private const int pirPin = 21;

        // Define GPIOs
        private GpioPin ledRed = null;
        private GpioPin ledGreen = null;
        private GpioPin pir = null;


        public void Run(IBackgroundTaskInstance taskInstance)
        {
            // 
            // TODO: Insert code to perform background work
            //
            // If you start any asynchronous methods here, prevent the task
            // from closing prematurely by using BackgroundTaskDeferral as
            // described in http://aka.ms/backgroundtaskdeferral
            //

            // Do not close application after startup
            _backgroundTaskDeferral = taskInstance.GetDeferral();

            deviceClient = DeviceClient.Create(iotHubUri, new DeviceAuthenticationWithRegistrySymmetricKey(deviceName, deviceKey));//commented due to: CS0104  C# is an ambiguous reference, TransportType.Http1

            // Execute first run
            GetSensorReadings();

            // Set task to be run in background
            ThreadPoolTimer.CreatePeriodicTimer(timer => {
                GetSensorReadings();
            }, new TimeSpan(0, 0, settings.timerCheckIntervall));

        }

        private void GetSensorReadings()
        {
            string pirCurrentState = "U";
            //EventData eventMessage;
            //Message message;
            MsgBody tmpMsg = new MsgBody(msg);

            try
            {
                // Initialize if needed
                if (pir == null)
                {  // Assume that initialization is needed
                    InitGPIO();
                }

                if (pir != null)
                {  // Only if we are intialized
                    // Get Current IP and MAC (May have changed!)
                    tmpMsg.ID = GetSensorId();
                    tmpMsg.Ip = GetCurrentIpv4Address();

                    // Check PIR Status (High == Movement)
                    // if it is high, then motion was detected
                    if (pir.Read() == GpioPinValue.High)
                    {
                        pirCurrentState = "O";
                    }
                    else
                    {
                        pirCurrentState = "F";
                    }

                    // Handle state changes with smoothing
                    if (pirCurrentState != state.PendingStatus || state.PendingStatus == "")
                    {  // A change. Start counting smoothing time. (Ie time with the same state required for change)
                        state.PendingStatusTime = DateTime.Now;
                        state.PendingStatus = pirCurrentState;
                    }

                    if (state.PendingStatus != tmpMsg.Status &&     // This is a Status change AND....
                                (tmpMsg.Status == "" ||             // this is the first run OR ...
                                (state.PendingStatus == "O" && (DateTime.Now - state.PendingStatusTime).TotalSeconds >= settings.timerStateOSmoothing) || // Pending status is "Occupied" and O-Smoothing-time has passed OR ...
                                (state.PendingStatus == "F" && (DateTime.Now - state.PendingStatusTime).TotalSeconds >= settings.timerStateFSmoothing)))
                    { // Pending status is "Free" and F-Smoothing-time has passed
                        if (state.PendingStatus == "O")
                        {
                            LedSetState(ledRed, true);  // Turn on RED LED
                        }
                        else
                        {
                            LedSetState(ledRed, false); // Turn off RED LED
                        }

                        tmpMsg.Status = state.PendingStatus;   // Set the new status to the message
                        tmpMsg.Change = "T"; // This is a change of status (T)rue

                        // Create brokered message
                        //eventMessage = new EventData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(tmpMsg)));
                        //*checking message in consoleTONE* Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, JsonConvert.SerializeObject(tmpMsg));
                        // Send to event hub
                        //_eventHub.Send(eventMessage);
                        SendDeviceToCloudMessagesAsync(tmpMsg);

                        // Save any changes (to this last so that any exception does not save changes.)
                        msg.setMsgBody(tmpMsg);
                        state.LastSendTime = DateTime.Now;

                        // Toggle green led to show a message has been sent
                        LedToggleGreen();
                    }
                    else if ((DateTime.Now - state.LastSendTime).TotalSeconds > settings.timerKeepAliveIntervall)
                    {  // No Change of status. Is it time fot "is alive"?
                        tmpMsg.Change = "F";   // Status change (F)alse. (Set if we will send (is alive)

                        // Create brokered message
                       // eventMessage = new EventData(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(tmpMsg)));

                        SendDeviceToCloudMessagesAsync(tmpMsg);
                        // Send to IoT hub
                        //private static async void SendDeviceToCloudMessagesAsync(Message m)
                        //{

                        //    await deviceClient.SendEventAsync(m);
                        //    Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, messageString);

                        //}

                       // _eventHub.Send(eventMessage);

                        // Save any changes (to this last so that any exception does not save changes.)
                        msg.setMsgBody(tmpMsg);
                        state.LastSendTime = DateTime.Now;

                        // Toggle green led to show a message has been sent
                        LedToggleGreen();
                    }
                }
            }
            catch (Exception exception)
            {
                //Status not written
            }
        }

        void LedSetState(GpioPin led, bool lit)
        {
            if (lit)
            {
                led.Write(GpioPinValue.Low);
            }
            else
            {
                led.Write(GpioPinValue.High);
            }
        }

        void LedToggleGreen()
        {
            state.LEDGreenOn = !state.LEDGreenOn;
            LedSetState(ledGreen, state.LEDGreenOn);
        }

        private void InitGPIO()
        {
            // get the GPIO controller
            var gpio = GpioController.GetDefault();

            // return an error if there is no gpio controller
            if (gpio == null)
            {
                ledRed = null;
                ledGreen = null;
                pir = null;
                // TODO? Log error message or GUI message here
                return;
            }

            // set up the LED on the defined GPIO pin
            // and set it to High to turn off the LED
            ledRed = gpio.OpenPin(ledRedPin);
            ledRed.Write(GpioPinValue.High);
            ledRed.SetDriveMode(GpioPinDriveMode.Output);

            ledGreen = gpio.OpenPin(ledGreenPin);
            ledGreen.Write(GpioPinValue.High);
            ledGreen.SetDriveMode(GpioPinDriveMode.Output);

            // set up the PIR sensor's signal on the defined GPIO pin
            // and set it's initial value to Low
            pir = gpio.OpenPin(pirPin);
            pir.SetDriveMode(GpioPinDriveMode.Input);

            // TODO: ?Log / Update screen about initialization
        }
         private static async void SendDeviceToCloudMessagesAsync(MsgBody tmpMsg)
         {
             var message = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(tmpMsg)));
             await deviceClient.SendEventAsync(message);

             Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, JsonConvert.SerializeObject(tmpMsg));

         }
        public static string GetCurrentIpv4Address()
        {
            var icp = NetworkInformation.GetInternetConnectionProfile();
            if (icp != null && icp.NetworkAdapter != null && icp.NetworkAdapter.NetworkAdapterId != null)
            {
                var name = icp.ProfileName;

                try
                {
                    var hostnames = NetworkInformation.GetHostNames();

                    foreach (var hn in hostnames)
                    {
                        if (hn.IPInformation != null &&
                            hn.IPInformation.NetworkAdapter != null &&
                            hn.IPInformation.NetworkAdapter.NetworkAdapterId != null &&
                            hn.IPInformation.NetworkAdapter.NetworkAdapterId == icp.NetworkAdapter.NetworkAdapterId &&
                            hn.Type == HostNameType.Ipv4)
                        {
                            return hn.CanonicalName;
                        }
                    }
                }
                catch (Exception)
                {
                    // do nothing
                    // in some (strange) cases NetworkInformation.GetHostNames() fails... maybe a bug in the API...
                }
            }

            return "Get IP Failed";
        }

        public static string GetSensorId()
        {
            // Possible limitations for a rollout: Is NetworkAdopterID changed when deployed to a new Pi3?
            // Possible future adoptions: Always get Wifi Adapter ID, Enable manual SensorID using Config file, Add IoT GUI to show / Set SensorID.
            var icp = NetworkInformation.GetInternetConnectionProfile();
            if (icp != null && icp.NetworkAdapter != null && icp.NetworkAdapter.NetworkAdapterId != null)
            {
                return icp.NetworkAdapter.NetworkAdapterId.ToString();
            }

            return "Get SensorID failed";
        }

        /*private string GetMacAddress() {
            const int MIN_MAC_ADDR_LENGTH = 12;
            string macAddress = string.Empty;
            long maxSpeed = -1;

            

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces()) {
                log.Debug(
                    "Found MAC Address: " + nic.GetPhysicalAddress() +
                    " Type: " + nic.NetworkInterfaceType);

                string tempMac = nic.GetPhysicalAddress().ToString();
                if (nic.Speed > maxSpeed &&
                    !string.IsNullOrEmpty(tempMac) &&
                    tempMac.Length >= MIN_MAC_ADDR_LENGTH) {
                    log.Debug("New Max Speed = " + nic.Speed + ", MAC: " + tempMac);
                    maxSpeed = nic.Speed;
                    macAddress = tempMac;
                }
            }

            return macAddress;
        }*/

    }
}


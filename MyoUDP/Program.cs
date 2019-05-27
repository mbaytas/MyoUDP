using System;
using System.Net.Sockets;
using System.Text;

using MyoSharp.Communication;
using MyoSharp.Device;
using MyoSharp.Poses;
using MyoSharp.Exceptions;

namespace MyoUDP
{
    class Program
    {
        // UDP settings
        private static string udpToIP = "192.168.1.107";
        private static int udpToPort = 57701;
        private static int udpFromPort = 11696;

        // Interaction stuff
        //// We will begin streaming when a 'fist' is recognized,
        //// and stream deltas of pitch, roll, yaw
        private static double initPitch = double.NaN;
        private static double initRoll = double.NaN;
        private static double initYaw = double.NaN;

        static void Main(string[] args)
        {
            Console.WriteLine("Make sure that Myo is worn, warmed up, and synced...");
            Console.WriteLine("Connecting to Myo and starting stream...");

            // Create a hub to manage Myo devices
            using (IChannel channel = Channel.Create(
                ChannelDriver.Create(ChannelBridge.Create(),
                MyoErrorHandlerDriver.Create(MyoErrorHandlerBridge.Create()))))
            using (IHub myoHub = Hub.Create(channel))
            {
                // Listen for when Myo connects
                myoHub.MyoConnected += (sender, e) =>
                {
                    Console.WriteLine("Connected to Myo {0}.", e.Myo.Handle);

                    // Unlock Myo so it doesn't keep locking between poses
                    e.Myo.Unlock(UnlockType.Hold);

                    // Say hello to Myo
                    e.Myo.Vibrate(VibrationType.Long);

                    // Listen for pose changes
                    e.Myo.PoseChanged += Myo_PoseChanged;

                    // Listen for lock/unlock
                    e.Myo.Locked += Myo_Locked;
                    e.Myo.Unlocked += Myo_Unlocked;
                };

                // Listen for when Myo disconnects
                myoHub.MyoDisconnected += (sender, e) =>
                {
                    Console.WriteLine("Disconnected from Myo {0}.", e.Myo.Handle);
                    e.Myo.PoseChanged -= Myo_PoseChanged;
                    e.Myo.Locked -= Myo_Locked;
                    e.Myo.Unlocked -= Myo_Unlocked;
                };

                channel.StartListening();

                // Keep running
                Console.WriteLine("Press ESC to quit.");
                if (Console.ReadKey(true).Key == ConsoleKey.Escape)
                {
                    myoHub.Dispose();
                    return;
                }
            }
        }

        private static void SendUDP(byte[] msgBytes)
        {
            using (UdpClient c = new UdpClient(udpFromPort))
            {
                c.Send(msgBytes, msgBytes.Length, udpToIP, udpToPort);
            }
        }


        private static void Myo_PoseChanged(object sender, PoseEventArgs e)
        {
            Console.WriteLine("MYO: Detected {1} pose!", e.Myo.Arm, e.Myo.Pose);

            if (e.Myo.Pose == Pose.Fist)
            {
                e.Myo.Vibrate(VibrationType.Short);
                initPitch = 0;
                initRoll = 0;
                initYaw = 0;

                byte[] msgBytes = { 1 };
                SendUDP(msgBytes);

                e.Myo.OrientationDataAcquired += Myo_OrientationDataAcquired;
            }
            else
            {
                if (!double.IsNaN(initPitch))
                {
                    e.Myo.Vibrate(VibrationType.Medium);
                }
                initPitch = double.NaN;
                initRoll = double.NaN;
                initYaw = double.NaN;

                byte[] msgBytes = { 0 };
                SendUDP(msgBytes);

                e.Myo.OrientationDataAcquired -= Myo_OrientationDataAcquired;
            }
        }

        private static void Myo_OrientationDataAcquired(object sender, OrientationDataEventArgs e)
        {
            // convert the values from (-PI, PI) to a 0-255 scale
            double pitch = (e.Pitch + Math.PI) / (Math.PI * 2.0f) * 256.0f;
            double roll = (e.Roll + Math.PI) / (Math.PI * 2.0f) * 256.0f;
            double yaw = (e.Yaw + Math.PI) / (Math.PI * 2.0f) * 256.0f;

            if (double.IsNaN(initPitch))
            {
                initPitch = e.Pitch;
                initRoll = e.Roll;
                initYaw = e.Yaw;
            }

            double deltaPitch = initPitch - e.Pitch;
            double deltaRoll = initRoll - e.Roll;
            double deltaYaw = initYaw - e.Yaw;

            // convert the values from (-PI, PI) to a 0-255 scale
            byte deltaPitchByte = (byte)((e.Pitch + Math.PI) / (Math.PI * 2.0f) * 256.0f);
            byte deltaRollByte = (byte)((e.Roll + Math.PI) / (Math.PI * 2.0f) * 256.0f);
            byte deltaYawByte = (byte)((e.Yaw + Math.PI) / (Math.PI * 2.0f) * 256.0f);

            byte[] msgBytes = { deltaPitchByte, deltaRollByte, deltaYawByte };

            SendUDP(msgBytes);
        }

        private static void Myo_Unlocked(object sender, MyoEventArgs e)
        {
            Console.WriteLine("MYO: Unlocked!", e.Myo.Arm);
        }

        private static void Myo_Locked(object sender, MyoEventArgs e)
        {
            Console.WriteLine("MYO: Locked!", e.Myo.Arm);
        }
    }
}

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Network
{
    public class ClientUdp : Client
    {
        private UdpClient udp;
        public ClientUdp(int bufferSize) : base(bufferSize) 
        { 
            
        }
        
        public override async Task<bool> Connect(string remoteIP, int remotePort)
        {
            try
            {
                if(connected)
                    Shutdown();
                udp = new UdpClient();
                //För att inte få ett error när man försöker skicka till en socket som blivit avstängd
                udp.Client.IOControl(-1744830452, new byte[1], new byte[1]);

                udp.Connect(remoteIP, remotePort);
                OnConnect(remoteIP, remotePort, (udp.Client.LocalEndPoint as IPEndPoint).Port);
                await Task.CompletedTask;
                return true;
            }
            catch(Exception)
            {
                Shutdown();
                return false;
            }
        }
        public override async Task<bool> Connect(string remoteIP, int remotePort, int localPort)
        {
            try
            {
                if(connected)
                    Shutdown();
                udp = new UdpClient(localPort);
                //För att inte få ett error när man försöker skicka till en socket som blivit avstängd
                udp.Client.IOControl(-1744830452, new byte[1], new byte[1]);

                udp.Connect(remoteIP, remotePort);
                OnConnect(remoteIP, remotePort, localPort);
                await Task.CompletedTask;
                return true;
            }
            catch(Exception)
            {
                Shutdown();
                return false;
            }
        }

        public override void Send(byte[] buffer, int size)
        {   
            udp.Send(buffer, size);
        }

        public override void SendFile(string file, byte[] preBuffer = null, byte[] postBuffer = null, TransmitFileOptions flags = 0)
        {
            udp.Client.SendFile(file, preBuffer, postBuffer, flags);
        }

        public override async Task ReceiveAsync()
        {
            var t = await udp.ReceiveAsync();
            onReceive?.Invoke(new ReceiveResult(t.Buffer, t.Buffer.Length, t.RemoteEndPoint, SocketType.Dgram));
        }

        public override void Shutdown()
        {
            if(udp != null)
            {
                udp.Dispose();
                udp.Close();
            }
            base.Shutdown();
        }
    }
}
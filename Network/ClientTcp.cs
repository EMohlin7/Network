using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Network
{
    public class ClientTcp : Client
    {        
        private TcpClient client;
        
        public ClientTcp(int bufferSize) : base(bufferSize) 
        {

        }
        
        public override async Task<bool> Connect(string remoteIP, int remotePort)
        {
            try
            {
                if(connected)
                    Shutdown();
                client = new TcpClient();
                await client.ConnectAsync(IPAddress.Parse(remoteIP), remotePort);
                OnConnect(remoteIP, remotePort, (client.Client.LocalEndPoint as IPEndPoint).Port);
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
                client = new TcpClient(new IPEndPoint(IPAddress.Any, localPort));
                await client.ConnectAsync(IPAddress.Parse(remoteIP), remotePort);
                OnConnect(remoteIP, remotePort, localPort);
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
            var stream = client.GetStream();
            stream.Write(buffer, 0, size);
        }

        public override void SendFile(string file, byte[] preBuffer = null, byte[] postBuffer = null, TransmitFileOptions flags = 0)
        {
           client.Client.SendFile(file, preBuffer, postBuffer, flags);
        }


        public override async Task ReceiveAsync()
        {
            byte[] buffer = new byte[bufferSize];
            int bytes = await client.Client.ReceiveAsync(buffer, SocketFlags.None);
            
            onReceive?.Invoke(new ReceiveResult(buffer, bytes, client.Client.RemoteEndPoint as IPEndPoint, SocketType.Stream));
        }
      

        public override void Shutdown()
        {
            Console.WriteLine("close on client");
            if(client != null)
            {
                client.Dispose();
                client.Close();
            }
            
            base.Shutdown();
        }
    }
}
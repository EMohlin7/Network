using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using TcpMethods;

namespace Network
{
    public class ClientTcp : Client
    {        
        public TcpClient client { private set; get; }
        
        public ClientTcp(int bufferSize) : base(bufferSize) 
        {

        }
        
        public override async Task<bool> Connect(string remoteIP, int remotePort)
        {
            try
            {
                if(connected)
                    Shutdown();
                client = new TcpClient(AddressFamily.InterNetwork);
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

        public override void Send(byte[] buffer, int bufferSize)
        {
            Tcp.Send(buffer, client, onSend);
        }
        public Task SendAsync(byte[] buffer, int bufferSize)
        {
            return Tcp.SendAsync(buffer, client, onSend);
        }

        public Task SendFile(string file, long offset, long? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
           return Tcp.SendFileAsync(file, client, bufferSize, onSend, offset, end, preBuffer, postBuffer);
        }


        public override async Task<ReceiveResult> ReceiveAsync()
        {
            return (await Tcp.ReceiveAsync(client, bufferSize, new System.Threading.CancellationToken())).CreateRegRR();
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
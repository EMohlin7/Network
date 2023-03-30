using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;
using TcpMethods;

namespace Network
{
    public class ClientTcp : Client
    {
        public TcpClient client { private set; get; }
        public virtual Stream GetStream() => client.GetStream();
        
        public ClientTcp(int bufferSize) : base(bufferSize) 
        {

        }
        public ClientTcp(int bufferSize, TcpClient client, bool connected) : base(bufferSize)
        {
            this.client = client;
            this.connected = connected;
        }

        public override async Task<bool> Connect(string host, int remotePort)
        {
            try
            {
                if (connected)
                    Shutdown();
                client = new TcpClient(AddressFamily.InterNetwork);
                await client.ConnectAsync(host, remotePort);
                OnConnect(new DnsEndPoint(host, (client.Client.RemoteEndPoint as IPEndPoint).Port), (client.Client.LocalEndPoint as IPEndPoint).Port);
                return true;
            }
            catch (Exception)
            {
                Shutdown();
                return false;
            }
        }
        public override async Task<bool> Connect(string host, int remotePort, int localPort)
        {
            try
            {
                if (connected)
                    Shutdown();
                client = new TcpClient(new IPEndPoint(IPAddress.Any, localPort));
                await client.ConnectAsync(host, remotePort);
                OnConnect(new DnsEndPoint(host, (client.Client.RemoteEndPoint as IPEndPoint).Port), localPort);
                return true;
            }
            catch (Exception)
            {
                Shutdown();
                return false;
            }
        }

        public override async Task<bool> Connect(IPAddress ip, int remotePort)
        {
            try
            {
                if(connected)
                    Shutdown();
                client = new TcpClient(AddressFamily.InterNetwork);
                await client.ConnectAsync(ip, remotePort);
                OnConnect(new DnsEndPoint(ip.ToString(), (client.Client.RemoteEndPoint as IPEndPoint).Port), (client.Client.LocalEndPoint as IPEndPoint).Port);
                return true;
            }
            catch(Exception) 
            {  
                Shutdown(); 
                return false;
            }
        }

        public override async Task<bool> Connect(IPAddress ip, int remotePort, int localPort)
        {
            try
            {
                if(connected)
                    Shutdown();
                client = new TcpClient(new IPEndPoint(IPAddress.Any, localPort));
                await client.ConnectAsync(ip, remotePort);
                OnConnect(new DnsEndPoint(ip.ToString(), (client.Client.RemoteEndPoint as IPEndPoint).Port), localPort);
                return true;
            }
            catch(Exception) 
            {  
                Shutdown(); 
                return false;
            }
        }

        public override void Send(byte[] buffer)
        {
            Tcp.Send(buffer, this, onSend);
        }
        public Task SendAsync(byte[] buffer)
        {
            return Tcp.SendAsync(buffer, this, onSend);
        }

        public Task SendFile(string file, long offset, long? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
           return Tcp.SendFileAsync(file, this, bufferSize, offset, end, onSend, preBuffer, postBuffer);
        }


        public override async Task<ReceiveResult> ReceiveAsync()
        {
            return (await Tcp.ReceiveAsync(this, bufferSize, new System.Threading.CancellationToken())).CreateRegRR();
        }
      

        public override void Shutdown()
        {
            if(client != null)
            {
                try
                {
                    GetStream().Dispose();
                }
                catch (InvalidOperationException) { }
                client.Dispose();
                client.Close();
            }
            
            base.Shutdown();
        }
    }
}
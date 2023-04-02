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
            udp.Client.ReceiveBufferSize = bufferSize;
            udp.Client.SendBufferSize = bufferSize;
        }

        public override async Task<bool> Connect(string host, int remotePort)
        {
            try
            {
                if (connected)
                    Shutdown();
                udp = new UdpClient();
                //För att inte få ett error när man försöker skicka till en socket som blivit avstängd
                udp.Client.IOControl(-1744830452, new byte[1], new byte[1]);

                udp.Connect(host, remotePort);
                OnConnect(new DnsEndPoint(host, remotePort), (udp.Client.LocalEndPoint as IPEndPoint).Port);
                await Task.CompletedTask;
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
                udp = new UdpClient(localPort);
                //För att inte få ett error när man försöker skicka till en socket som blivit avstängd
                udp.Client.IOControl(-1744830452, new byte[1], new byte[1]);

                udp.Connect(host, remotePort);
                OnConnect(new DnsEndPoint(host, remotePort), (udp.Client.LocalEndPoint as IPEndPoint).Port);
                await Task.CompletedTask;
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
                udp = new UdpClient();
                //För att inte få ett error när man försöker skicka till en socket som blivit avstängd
                udp.Client.IOControl(-1744830452, new byte[1], new byte[1]);

                udp.Connect(ip, remotePort);
                OnConnect(new DnsEndPoint(ip.ToString(), remotePort), (udp.Client.LocalEndPoint as IPEndPoint).Port);
                await Task.CompletedTask;
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
                udp = new UdpClient(localPort);
                //För att inte få ett error när man försöker skicka till en socket som blivit avstängd
                udp.Client.IOControl(-1744830452, new byte[1], new byte[1]);

                udp.Connect(ip, remotePort);
                OnConnect(new DnsEndPoint(ip.ToString(), remotePort), (udp.Client.LocalEndPoint as IPEndPoint).Port);
                await Task.CompletedTask;
                return true;
            }
            catch(Exception)
            {
                Shutdown();
                return false;
            }
        }

        public override void Write(byte[] buffer)
        {   
            udp.Send(buffer, buffer.Length);
            onSend?.Invoke(buffer.LongLength, udp.Client.RemoteEndPoint as IPEndPoint);
        }
        public override async Task WriteAsync(byte[] buffer)
        {
            await udp.SendAsync(buffer, buffer.Length);
            onSend?.Invoke(buffer.LongLength, udp.Client.RemoteEndPoint as IPEndPoint);
        }



        public override async Task<ReceiveResult> ReceiveAsync()
        {
            var t = await udp.ReceiveAsync();
            return new ReceiveResult(t.Buffer, t.Buffer.Length, t.RemoteEndPoint, SocketType.Dgram);
        }
        public override ReceiveResult Receive()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
            byte[] bytes = udp.Receive(ref ep);
            return new ReceiveResult(bytes, bytes.Length, ep, SocketType.Dgram);
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
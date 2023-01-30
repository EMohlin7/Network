using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Network
{
    public class ServerUdp : Server
    {
        private UdpClient udp;
        public ServerUdp(int bufferSize) : base(bufferSize, 1) { }
        
        public override bool StartListening(int port)
        {
            //Check if the server is already listening
            if(udp != null)
            {
                Shutdown();
                return true;
            }
            
            try{
                udp = new UdpClient(port, AddressFamily.InterNetwork);
            }
            catch{
                udp = null; 
                return false;
            }

            //För att inte få ett error när man försöker skicka till en socket som blivit avstängd
            udp.Client.IOControl(-1744830452, new byte[1], new byte[1]);
            return true;
        }

        public async Task<ReceiveResult> ReceiveAsync()
        {
            var t = await udp.ReceiveAsync();
            return new ReceiveResult(t.Buffer, t.Buffer.Length, t.RemoteEndPoint, SocketType.Dgram);
        }

        public override void Send(byte[] buffer, IPEndPoint ep)
        {
            udp.Send(buffer, buffer.Length, ep);
            onSend?.Invoke(buffer.LongLength, ep);
        }

        public override void Shutdown()
        {
            udp.Dispose();
            udp.Close();
        }
    }
}

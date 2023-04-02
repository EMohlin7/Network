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

        /// <summary>
        /// Start listening for incoming connections
        /// </summary>
        /// <param name="port">The port to listen on</param>
        /// <param name="err">Returns the error if the server failed to start, otherwise null</param>
        /// <returns>True if the server succesfully started listening for incoming connections, otherwise false</returns>
        public override bool StartListening(int port, out string err)
        {
            err = null;
            //Check if the server is already listening
            if(udp != null)
            {
                Shutdown();
                return true;
            }
            
            try{
                udp = new UdpClient(port, AddressFamily.InterNetwork);
            }
            catch(Exception e)
            {
                err = e.Message;
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

        public override void Write(byte[] buffer, IPEndPoint ep)
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

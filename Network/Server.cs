using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Network
{
    public struct ReceiveResult
    {
        public static ReceiveResult Failed()
        {
            return new ReceiveResult(new byte[0], 0, null, SocketType.Unknown, false);
        }

        public ReceiveResult(byte[] buffer, int size, IPEndPoint remoteEndPoint, SocketType socketType, bool success = true)
        {
            this.buffer = buffer;
            this.size = size;
            this.remoteEndPoint = remoteEndPoint;
            this.socketType = socketType;
            this.success = success;
        }
        public readonly bool success;
        public readonly byte[] buffer;
        public readonly int size;
        public readonly IPEndPoint remoteEndPoint;
        public readonly SocketType socketType;
    }

    public abstract class Server
    {
        //public Action<ReceiveResult> onReceive;
        public Action<long, IPEndPoint> onSend;
        protected int bufferSize {private set; get;}

        public Server(int bufferSize, int maxConnections)
        {
            this.bufferSize = bufferSize;
        }

        /// <summary>
        /// Start listening for incoming connections
        /// </summary>
        /// <param name="port">The port to listen on</param>
        /// <param name="err">Returns the error if the server failed to start, otherwise null</param>
        /// <returns>True if the server succesfully started listening for incoming connections, otherwise false</returns>
        public abstract bool StartListening(int port, out string err);
        

        public void SendToMultiple(byte[] buffer, IPEndPoint[] ep)
        {
            for(int i = 0; i < ep.Length; ++i)
            {
                Send(buffer, ep[i]);
            }
        }

        
        public abstract void Send(byte[] buffer, IPEndPoint ep);
        
    
        public abstract void Shutdown();
    }
}

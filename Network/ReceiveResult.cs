using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
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


    public struct TcpReceiveResult
    {
        public ReceiveResult CreateRegRR()
        {
            return new ReceiveResult(buffer, size, client.client.Client.RemoteEndPoint as IPEndPoint, socketType, success);
        }
        public static TcpReceiveResult Failed(ClientTcp client)
        {
            return new TcpReceiveResult(ReceiveResult.Failed(), client);
        }
        public TcpReceiveResult(byte[] buffer, int size, ClientTcp client, bool success = true) 
        {
            this.client = client;
            this.buffer = buffer;
            this.size = size;
            this.success = success;
            this.socketType = SocketType.Stream;
        }
        public TcpReceiveResult(ReceiveResult rr, ClientTcp client)
        {
            this.client = client;
            success= rr.success;
            buffer= rr.buffer;
            size= rr.size;
            this.socketType = rr.socketType;
        }

        public readonly bool success;
        public readonly byte[] buffer;
        public readonly int size;
        public readonly SocketType socketType;
        public readonly ClientTcp client;
    }
}

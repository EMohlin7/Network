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
            return new ReceiveResult(new byte[0], 0, null, SocketType.Unknown, false, false);
        }

        public ReceiveResult(byte[] buffer, int size, IPEndPoint remoteEndPoint, SocketType socketType, bool remainingData, bool success = true)
        {
            this.buffer = buffer;
            this.size = size;
            this.remoteEndPoint = remoteEndPoint;
            this.socketType = socketType;
            this.success = success;
            this.remainingData = remainingData;
        }
        public readonly bool success;
        public readonly bool remainingData;
        public readonly int size;
        public readonly byte[] buffer;
        public readonly SocketType socketType;
        public readonly IPEndPoint remoteEndPoint;
    }


    
}

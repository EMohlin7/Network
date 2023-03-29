using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Network
{
    public abstract class Client
    {
        public IPEndPoint remoteEndpoint {private set; get;}
        public int simulatedServerToClientLatency;
        public int localPort {private set; get;}
        public bool connected {protected set; get;}
        protected int bufferSize {private set; get;}
        public Action onShutdown;
        public Action<long, IPEndPoint> onSend;

        public Client(int bufferSize)
        {
            this.bufferSize = bufferSize;
        }

        public abstract Task<bool> Connect(string remoteIP, int remotePort);
        public abstract Task<bool> Connect(string remoteIP, int remotePort, int localPort);
        protected void OnConnect(string remoteIP, int remotePort, int localPort)
        {
            remoteEndpoint = new IPEndPoint(IPAddress.Parse(remoteIP), remotePort);
            this.localPort = localPort;
            connected = true;
        }

        public abstract Task<ReceiveResult> ReceiveAsync();
        public abstract void Send(byte[] buffer);

        //public abstract void SendFile(string file, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags);

        public virtual void Shutdown()
        {
            connected = false;
            onShutdown?.Invoke();
        }
    }
}
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Network
{
    public abstract class Client
    {
        public DnsEndPoint remoteEndpoint {private set; get;}
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

        public abstract Task<bool> Connect(string host, int remotePort);
        public abstract Task<bool> Connect(string host, int remotePort, int localPort);
        public abstract Task<bool> Connect(IPAddress remoteIP, int remotePort);
        public abstract Task<bool> Connect(IPAddress remoteIP, int remotePort, int localPort);
        protected virtual void OnConnect(DnsEndPoint remoteEP, int localPort)
        {
            remoteEndpoint = remoteEP;
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
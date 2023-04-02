using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Network
{
    public abstract class Client
    {
        public DnsEndPoint remoteEndpoint {private set; get;}
        public int localPort {private set; get;}
        public bool connected {protected set; get;}
        private int _bufSize;
        public int bufferSize { private set { _bufSize = value > 0 ? value : 1; bufSizeChanged?.Invoke(value); } get{ return _bufSize; } }
        public Action<int> bufSizeChanged;
        public Action onShutdown;
        public Action<long, IPEndPoint> onSend;
        public Action<DnsEndPoint> onConnect;

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
            onConnect?.Invoke(remoteEP);
        }

        public abstract ReceiveResult Receive();
        public abstract Task<ReceiveResult> ReceiveAsync();
        public abstract void Write(byte[] buffer);
        public abstract Task WriteAsync(byte[] buffer);


        public virtual void Shutdown()
        {
            connected = false;
            onShutdown?.Invoke();
        }
    }
}
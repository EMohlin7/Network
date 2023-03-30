using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Network
{
    public  class ClientTcpSSL : ClientTcp
    {
        protected SslStream sslStream;
        public override Stream GetStream() => sslStream;
     
        
        public ClientTcpSSL(int bufferSize) : base(bufferSize)
        {

        }
        public ClientTcpSSL(TcpClient client, SslStream stream, bool connected, int bufferSize) : base(bufferSize, client, connected) 
        {
            this.sslStream = stream;
        }

        public override Task<bool> Connect(IPAddress ip, int remotePort)
        {
            return Task<bool>.FromResult(false);
        }
        public override Task<bool> Connect(IPAddress ip, int remotePort, int localPort)
        {
            return Task<bool>.FromResult(false);
        }

        protected override void OnConnect(DnsEndPoint remoteEP, int localPort)
        {
            sslStream = new SslStream(client.GetStream());
            sslStream.AuthenticateAsClient(remoteEP.Host);
            base.OnConnect(remoteEP, localPort);
        }

    }
}

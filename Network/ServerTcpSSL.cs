using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Network
{
    public class ServerTcpSSL : ServerTcp
    {
        private X509Certificate cert;
        public ServerTcpSSL(X509Certificate2 cert, int maxConnections, int bufferSize) : base(maxConnections, bufferSize)
        {
            this.cert = cert;
        }

        protected override void Accept(object clientTcp)
        {
            ClientTcp client = (ClientTcp)clientTcp;
            SslStream sslStream = new SslStream(client.GetStream(), false);
            sslStream.AuthenticateAsServer(cert, clientCertificateRequired: false, checkCertificateRevocation: true);
            ClientTcpSSL sslClient = new ClientTcpSSL(client.client, sslStream, true, bufferSize);
            
            base.Accept(sslClient);
        }
    }
}

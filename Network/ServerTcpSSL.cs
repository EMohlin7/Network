using System;
using System.Collections.Generic;
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
        public ServerTcpSSL(string certFile, int maxConnections, int bufferSize) : base(maxConnections, bufferSize)
        {
            cert = new X509Certificate(certFile);
        }

        protected override void Accept(object tcpClient)
        {
            TcpClient client = (TcpClient)tcpClient;
            SslStream sslStream = new SslStream(client.GetStream(), false);
            sslStream.AuthenticateAsServer(cert, clientCertificateRequired: false, checkCertificateRevocation: true);


            base.Accept(tcpClient);
        }
    }
}

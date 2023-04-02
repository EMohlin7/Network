﻿using System;
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
        public ClientTcpSSL(int bufferSize, bool buffered) : base(bufferSize, buffered)
        {

        }
        public ClientTcpSSL(TcpClient client, SslStream stream, bool connected, int bufferSize, bool buffered)
            : base(bufferSize, client, stream, connected, buffered) 
        {
            
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
            SslStream sslStream = new SslStream(client.GetStream());
            sslStream.AuthenticateAsClient(remoteEP.Host);
            this.stream = sslStream;
            base.OnConnect(remoteEP, localPort);
        }

    }
}

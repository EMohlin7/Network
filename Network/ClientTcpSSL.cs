using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Network
{
    public  class ClientTcpSSL : ClientTcp
    {
        protected SslStream stream;
        public override Stream GetStream() => stream;
     
          
        public ClientTcpSSL(TcpClient client, SslStream stream, bool connected, int bufferSize) : base(bufferSize, client, connected) 
        {
            this.stream = stream;
        }

        public override Task<bool> Connect(string remoteIP, int remotePort)
        {
            return base.Connect(remoteIP, remotePort);
        }
        public override Task<bool> Connect(string remoteIP, int remotePort, int localPort)
        {
            return base.Connect(remoteIP, remotePort, localPort);
        }
        
    }
}

using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text; 

namespace TestProject1
{
    [TestClass]
    public class SSLTest
    {
        const int bufferSize = 4096;
        const int sPort = 8081;
        private ServerTcpSSL CreateServer()
        {
            string privKey = "C:\\Users\\edvin\\Documents\\c#\\dlls\\Network\\TestProject1\\privkey.pem";
            string certFile = "C:\\Users\\edvin\\Documents\\c#\\dlls\\Network\\TestProject1\\fullchain.pem";
            var cert = X509Certificate2.CreateFromPemFile(certFile, privKey);
            return new ServerTcpSSL(new X509Certificate2(cert.Export(X509ContentType.Pkcs12)), 10, bufferSize, true);
        }

        [TestMethod]
        public void CertTest()
        {
            
            ServerTcpSSL server = CreateServer();
            bool success = server.StartListening(sPort, out string err);
            server.Shutdown();
            Assert.IsTrue(success);
        }
        [TestMethod]
        public async Task ServerAndClientTest()
        {
            string msg = "Hejsan";
            ServerTcpSSL server = CreateServer();
            if (!server.StartListening(sPort, out string err))
            {
                Console.WriteLine(err);
                Assert.Fail();
            }
            ClientTcpSSL client = new ClientTcpSSL(bufferSize, true);
            if(!await client.Connect("edvinmohlin.se", 443))
            {
                Console.WriteLine(err);
                Assert.Fail();
            }
            client.buffered = true;
            client.onSend += OnSend;
            await client.WriteAsync(Encoding.UTF8.GetBytes(msg));
            Console.WriteLine("Not Sent");

            while (server.numWaitingClients == 0)
            {

            }
            
            if(!server.FetchWaitingClient(out ClientTcp c, -1))
                Assert.Fail();

            ClientTcpSSL sClient = c as ClientTcpSSL;
            client.Flush();
            var rr = await sClient.ReceiveAsync();
            string rec = Encoding.UTF8.GetString(rr.buffer);
            server.Shutdown();
            client.Shutdown();
            Assert.AreEqual(msg, rec);
        }

        private void OnSend(long bytes, IPEndPoint ep)
        {
            Console.WriteLine("Sent {0} bytes to {1}", bytes, ep);
        }

    }
}
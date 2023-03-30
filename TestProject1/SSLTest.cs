using System.Security.Cryptography.X509Certificates;
using System.Text; 

namespace TestProject1
{
    [TestClass]
    public class SSLTest
    {
        const int bufferSize = 4096;
        const int sPort = 443;
        private ServerTcpSSL CreateServer()
        {
            string privKey = "C:\\Users\\edvin\\Documents\\c#\\dlls\\Network\\TestProject1\\privkey.pem";
            string certFile = "C:\\Users\\edvin\\Documents\\c#\\dlls\\Network\\TestProject1\\fullchain.pem";
            var cert = X509Certificate2.CreateFromPemFile(certFile, privKey);
            return new ServerTcpSSL(new X509Certificate2(cert.Export(X509ContentType.Pkcs12)), 10, bufferSize);
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
            if (!server.StartListening(443, out string err))
            {
                Console.WriteLine(err);
                Assert.Fail();
            }
            ClientTcpSSL client = new ClientTcpSSL(bufferSize);
            if(!await client.Connect("edvinmohlin.se", sPort))
            {
                Console.WriteLine(err);
                Assert.Fail();
            }

            await client.SendAsync(Encoding.UTF8.GetBytes(msg));

            while (server.numWaitingClients == 0)
            {

            }
            
            if(!server.FetchWaitingClient(out ClientTcp sClient, -1))
                Assert.Fail();
            var rr = await sClient.ReceiveAsync();
            string rec = Encoding.UTF8.GetString(rr.buffer);
            server.Shutdown();
            client.Shutdown();
            Assert.AreEqual(msg, rec);
        }

    }
}
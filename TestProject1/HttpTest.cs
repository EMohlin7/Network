using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using HTTPParser;

namespace TestProject1
{
    [TestClass]
    public class HttpTest
    {
        private HttpServer server;
        private HttpServer s;
        ManualResetEvent w = new ManualResetEvent(false);
        [TestMethod]
        public void TestHttp()
        {
            s = new HttpServer(null, 10, 4096, true);
            s.Get("/test", OnTest);
            //s.StartServer(80, true);
            server = CreateServer();
            server.Get("/test", OnTest);
            
            server.StartServer(443, true);
            w.WaitOne();
            server.StopServer();
            Assert.IsTrue(true);
        }

        

        private void OnTest(Request req, ReceiveResult rr, ClientTcp client)
        {
            Console.WriteLine(req.element);
            w.Set();
        }

        private HttpServer CreateServer()
        {
            string privKey = "C:\\Users\\edvin\\Documents\\c#\\dlls\\Network\\TestProject1\\privkey.pem";
            string certFile = "C:\\Users\\edvin\\Documents\\c#\\dlls\\Network\\TestProject1\\fullchain.pem";
            var cert = X509Certificate2.CreateFromPemFile(certFile, privKey);
            return new HttpServer(null, 10, 4096, false, new X509Certificate2(cert.Export(X509ContentType.Pkcs12)));
        }
    }
}

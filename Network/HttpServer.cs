using HTTPParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Unicode;
using System.Threading;

using System.Threading.Tasks;

namespace Network
{
    public class HttpServer
    {
        public string fileDirectory;
        public ServerTcp server;
        public readonly bool encrypted = false;

        public delegate void requestHandler(Request request, ReceiveResult receiveResult, ClientTcp client);

        public Action<ReceiveResult, Request> receivedRequest;

        public Dictionary<string, requestHandler> getDict;
        public Dictionary<string, requestHandler> headDict;
        public Dictionary<string, requestHandler> postDict;
        public Dictionary<string, requestHandler> putDict;
        public Dictionary<string, requestHandler> deleteDict;
        public Dictionary<string, requestHandler> connectDict;
        public Dictionary<string, requestHandler> optionsDict;
        public Dictionary<string, requestHandler> traceDict;
        public Dictionary<string, requestHandler> patchDict;

        private Dictionary<string, Dictionary<string, requestHandler>> methods;


        private void Constructor(string fileDirectory, int maxConnections, int bufferSize, bool buffered)
        {
            methods = new Dictionary<string, Dictionary<string, requestHandler>>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"GET", getDict },
                {"HEAD", headDict },
                {"POST", postDict},
                {"PUT", putDict},
                {"DELETE", deleteDict},
                {"CONNECT", connectDict},
                {"OPTIONS", optionsDict},
                {"TRACE", traceDict},
                {"PATCH", patchDict}
            };
        }
        public HttpServer(string fileDirectory, int maxConnections, int bufferSize, bool buffered)
        {
            this.fileDirectory = fileDirectory;
            server = new ServerTcp(maxConnections, bufferSize, buffered);
        }

        public HttpServer(string fileDirectory, int maxConnections, int bufferSize, bool buffered, X509Certificate2 sslCert)
        {
            this.fileDirectory = fileDirectory;
            server = new ServerTcpSSL(sslCert, maxConnections, bufferSize, buffered);
        }

        public void StartServer(int port)
        {
            Thread t = new Thread(_startServer);
            t.IsBackground = false;
            t.Start(port);
        }

        public void _startServer(object port)
        {
            server.StartListening((int)port, out string err);
            server.clientAccepted += OnClientAccepted;
            
            
        }

        private void OnClientAccepted()
        {
            if (!server.FetchWaitingClient(out ClientTcp client, -1))
                return;

            if (client != null) { return; }
            ReceiveResult rr;
            do
            {

                byte[] result = new byte[0];
                do
                {
                    rr = client.Receive();
                    long currLenght = result.LongLength;
                    result = new byte[currLenght + rr.size];
                    rr.buffer.CopyTo(result, currLenght);
                } while (rr.size != 0);
                rr = new ReceiveResult(rr.buffer, rr.size, rr.remoteEndPoint, rr.socketType, rr.success);
            } while (OnReceive(rr, client));
        
            server.CloseClientSocket(client, -1);
        }

        //Returns if the connection should stay open
        private bool OnReceive(ReceiveResult rr, ClientTcp client)
        {

            //size==0 Means that the client sent an empty message which often indicates end of transmission in this case
            if (rr.size == 0)
            {
                return false;
            }
            
            //TODO: Change this
            string receivedMsg = Encoding.UTF8.GetString(rr.buffer);
            
            Request req = new Request(receivedMsg);
            if (req == null)
                return false;

            receivedRequest?.Invoke(rr, req);

            
            bool keepAlive = req.TryGetHeader("Connection", out string con) && con == "keep-alive";
           
            var res = new Response(200);


            if (!methods.TryGetValue(req.method, out var requestHandlers))
                return false;


            try
            {
                if (requestHandlers.TryGetValue(req.element, out requestHandler value))
                    value.Invoke(req, rr, client);
                else
                {
                    if (!VerifyPathInDirectory(req.element))
                        throw new DirectoryNotFoundException($"The requested element: {req.element} was outside the given directory");

                    if (keepAlive)
                    {
                        res.SetHeader("Connection", "keep-alive");
                        res.SetHeader("Content-Length", GetFileSize(fileDirectory + req.element).ToString());
                    }
                    client.WriteFile(fileDirectory + req.element, 0, null, Encoding.UTF8.GetBytes(res.GetMsg()));
                }
                
            }
            catch (Exception e) when (ExceptionFilter(e, client, rr)) { return false; }
            finally { client.Flush(); }

            return keepAlive;
        }
        private static bool ExceptionFilter(Exception e, ClientTcp client, ReceiveResult rr)
        {
            if (e is SocketException)
            {
                var s = e as SocketException;
                Console.WriteLine("Socket exception code: " + s.ErrorCode);
                return true;
            }
            else if (e is DirectoryNotFoundException || e is FileNotFoundException || e is UnauthorizedAccessException)
            {
                byte[] code = System.Text.Encoding.UTF8.GetBytes("HTTP/1.1 404 not found \r\n\r\n");
                client.Write(code);
                Console.WriteLine(e.Message);
                return true;
            }

            return false;
        }

        private bool VerifyPathInDirectory(string pathToVerify)
        {
            var fullRoot = Path.GetFullPath(fileDirectory);
            var fullPathToVerify = Path.GetFullPath(pathToVerify);
            return fullPathToVerify.StartsWith(fullRoot);
        }

        private long GetFileSize(string file)
        {
            return new FileInfo(file).Length;
        }
    }
}

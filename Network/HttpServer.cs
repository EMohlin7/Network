﻿using HTTPParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;


namespace Network
{
    public class HttpServer
    {
#nullable enable
        public string? fileDirectory;
        public ServerTcp server;
        public readonly bool encrypted = false;

        private AutoResetEvent threadWait = new AutoResetEvent(false);

        public delegate void requestHandler(Request request, ReceiveResult receiveResult, ClientTcp client);

        public Action<ReceiveResult, Request>? receivedRequest;

        public Dictionary<string, requestHandler> getDict = new Dictionary<string, requestHandler>();
        public Dictionary<string, requestHandler> headDict = new Dictionary<string, requestHandler>();
        public Dictionary<string, requestHandler> postDict = new Dictionary<string, requestHandler>();
        public Dictionary<string, requestHandler> putDict = new Dictionary<string, requestHandler>();
        public Dictionary<string, requestHandler> deleteDict = new Dictionary<string, requestHandler>();
        public Dictionary<string, requestHandler> connectDict = new Dictionary<string, requestHandler>();
        public Dictionary<string, requestHandler> optionsDict = new Dictionary<string, requestHandler>();
        public Dictionary<string, requestHandler> traceDict = new Dictionary<string, requestHandler>();
        public Dictionary<string, requestHandler> patchDict = new Dictionary<string, requestHandler>();

        private Dictionary<string, Dictionary<string, requestHandler>> methods = new Dictionary<string, Dictionary<string, requestHandler>>();




        private void Constructor(string? fileDirectory, int maxConnections, int bufferSize)
        {
            this.fileDirectory = fileDirectory;

            //Not case sensitive
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
        public HttpServer(string? fileDirectory, int maxConnections, int bufferSize, bool buffered)
        {
            encrypted = false;
            server = new ServerTcp(maxConnections, bufferSize, buffered);
            Constructor(fileDirectory, maxConnections, bufferSize);
        }

        public HttpServer(string? fileDirectory, int maxConnections, int bufferSize, bool buffered, X509Certificate2 sslCert)
        {
            server = new ServerTcpSSL(sslCert, maxConnections, bufferSize, buffered);
            encrypted = true;
            Constructor(fileDirectory, maxConnections, bufferSize);
        }
#nullable disable
        public void StartServer(int port, bool background)
        {
            Thread t = new Thread(_startServer);
            t.IsBackground = background;
            t.Start(port);
        }

        private void _startServer(object port)
        {
            server.StartListening((int)port, out string err);
            server.clientAccepted += OnClientAccepted;
            threadWait.WaitOne();
        }

        public void StopServer()
        {
            server.Shutdown();
            threadWait.Set();
        }

        private void OnClientAccepted()
        {
            if (!server.FetchWaitingClient(out ClientTcp client, -1))
                return;

            if (client == null) { return; }
            ReceiveResult rr;
            do
            {
                byte[] result = new byte[0];
                do
                {
                    rr = client.Receive();
                    long currLenght = result.LongLength;
                    result = new byte[currLenght + rr.size];
                    Array.Copy(rr.buffer, 0, result, currLenght, rr.size);
                } while (rr.remainingData);
                rr = new ReceiveResult(result, result.Length, rr.remoteEndPoint, rr.socketType, rr.success);
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

            string receivedMsg = Encoding.UTF8.GetString(rr.buffer);
            
            if (!Request.TryParseMsg(receivedMsg, out Request req))
                return false;

            receivedRequest?.Invoke(rr, req);

            if (!methods.TryGetValue(req.method, out var requestHandlers))
                return false;

            bool keepAlive = req.TryGetHeader("Connection", out string con) && con == "keep-alive";
            try
            {
                //First check if everything should be handled by one delegate
                if (requestHandlers.TryGetValue("*", out requestHandler value))
                    value.Invoke(req, rr, client);
                else if (requestHandlers.TryGetValue(req.element, out value))
                    value.Invoke(req, rr, client);
                else if (req.method.ToLower() == "get" && fileDirectory != null)
                {
                    var res = new Response(200);
                    FileInfo fi = new FileInfo(fileDirectory + req.element);
                    long fileLength = fi.Length;
                    if (keepAlive)
                    {
                        res.SetHeader("Connection", "keep-alive");
                    }
                    res.SetHeader("Content-Length", fileLength.ToString());
                    res.SetHeader("Content-Type", GetTypeDir(req)+$"/{fi.Extension.TrimStart('.')}");
                    res.SetHeader("Accept-Ranges", "bytes");
                    GetRange(req, out long start, out long? end);
                    
                    res.SetHeader("Content-Range", string.Format("bytes {0}-{1}/{2}", start, end ?? fileLength - 1, fileLength));
                    client.WriteFile(fileDirectory + req.element, start, end, Encoding.UTF8.GetBytes(res.GetMsg()));
                }
                else
                {
                    Send404(client);
                }
            }
            catch (Exception e) when (ExceptionFilter(e, client, rr)) { return false; }
            finally { try { if(client.buffered) client.Flush(); } catch (IOException) { } }

            return keepAlive;
        }
        private static bool ExceptionFilter(Exception e, ClientTcp client, ReceiveResult rr)
        {
            if (e is SocketException)
            {
                return true;
            }
            else if (e is DirectoryNotFoundException || e is FileNotFoundException || e is UnauthorizedAccessException)
            {
                try
                {
                    Send404(client);
                }
                catch (Exception) { }
                return true;
            }

            return false;
        }

        private static void Send404(ClientTcp client)
        {
            byte[] code = System.Text.Encoding.UTF8.GetBytes("HTTP/1.1 404 not found \r\n\r\n");
            client.Write(code);
        }

        //Default is text
        private string GetTypeDir(Request req)
        {
            string[] s = req.method.Split('/');
            if (s.Length > 1)
                return s[0];
            else
                return "text";
        }

        private bool GetRange(Request req, out long start, out long? end)
        {
            start = 0;
            end = null;
            if (!req.HeaderExists("range"))
            {
                return false;
            }

            string[] bytes = req.GetHeader("range").Split("=")[1].Split("-");
            if (!long.TryParse(bytes[0], out long val))
            {
                return false;
            }
            start = val;
            if (bytes[1].Length > 0 && long.TryParse(bytes[1], out val))
                end = val;
            else
                end = null;

            return true;
        }

        private bool VerifyPathInDirectory(string pathToVerify)
        {
            if (fileDirectory == null)
                return false;
            var fullRoot = Path.GetFullPath(fileDirectory);
            var fullPathToVerify = Path.GetFullPath(pathToVerify);
            return fullPathToVerify.StartsWith(fullRoot);
        }

        private long GetFileSize(string file)
        {
            return new FileInfo(file).Length;
        }

        public void CloseClient(ClientTcp client, int timeoutMilliseconds)
        {
            server.CloseClientSocket(client, timeoutMilliseconds);
        }

        public void Get(string element, requestHandler handler)
        {
            getDict.Add(element, handler);
        }
        public void Head(string element, requestHandler handler)
        {
            headDict.Add(element, handler);
        }
        public void Post(string element, requestHandler handler)
        {
            postDict.Add(element, handler);
        }
        public void Put(string element, requestHandler handler)
        {
            putDict.Add(element, handler);
        }
        public void Delete(string element, requestHandler handler)
        {
            deleteDict.Add(element, handler);
        }
        public void Connect(string element, requestHandler handler)
        {
            connectDict.Add(element, handler);
        }
        public void Options(string element, requestHandler handler)
        {
            optionsDict.Add(element, handler);
        }
        public void Trace(string element, requestHandler handler)
        {
            traceDict.Add(element, handler);
        }
        public void Patch(string element, requestHandler handler)
        {
            patchDict.Add(element, handler);
        }
    }
}

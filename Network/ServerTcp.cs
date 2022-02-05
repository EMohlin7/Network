using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.IO;

namespace Network
{
    public class ServerTcp : Server
    {
        private TcpListener listener;   
        public int connectedClients {get => clients.Count;}
        private List<TcpClient> clients;      
        public Action<TcpClient> onClientClosed;
        public Action<TcpClient> onAccept;
        public bool listening {private set; get;}
        public int maxConnections;

#region Start
        
        public ServerTcp(int maxConnections, int bufferSize) : base(bufferSize, maxConnections)
        {
            this.maxConnections = maxConnections;
            clients = new List<TcpClient>();
        }

        public override async Task StartListening(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start(maxConnections);
            listening = true;
            await StartAccept(listener);
        }
#endregion

#region Accept
        private async Task StartAccept(TcpListener listener)
        {
            try
            {
                while(true)
                {
                    if(listening && connectedClients < maxConnections)
                    {
                        var client = await listener.AcceptTcpClientAsync();
                        Accept(client);
                    }
                }
            }
            catch (ObjectDisposedException){return;}
            catch(SocketException){return;}
        }

        private void Accept(TcpClient client)
        {
            clients.Add(client); 
            //Interlocked.Increment(ref connectedClients);   
            onAccept?.Invoke(client);
        }
#endregion

#region Receive
        
        public Task<ReceiveResult> ReceiveAsync(IPEndPoint ep)
        {
            return ReceiveAsync(GetClient(ep), new CancellationToken());
        }
        public Task<ReceiveResult> ReceiveAsync(IPEndPoint ep, CancellationToken token)
        {
            return ReceiveAsync(GetClient(ep), token);
        }

        public async Task<ReceiveResult> ReceiveAsync(TcpClient client)
        {
            if(client == null)
                return ReceiveResult.Failed();
            return await ReceiveAsync(client, new CancellationToken());
        }
        public async Task<ReceiveResult> ReceiveAsync(TcpClient client, CancellationToken token)
        { 
            byte[] buffer = new byte[bufferSize];
            var stream = client.GetStream();
            try{
                int bytes = await stream.ReadAsync(buffer, 0, bufferSize, token);
                byte[] received = new byte[bytes];
                Array.Copy(buffer, received, bytes);
                return (new ReceiveResult(received, bytes, client.Client.RemoteEndPoint as IPEndPoint, SocketType.Stream));
            }
            catch(System.IO.IOException){return ReceiveResult.Failed();}
        }
#endregion

#region Send
        public override void Send(byte[] buffer, IPEndPoint ep)
        {
            var client = GetClient(ep);
            if(client == null)
                return;
            var stream = client.GetStream();
            stream.Write(buffer, 0, buffer.Length);
            base.Send(buffer,ep);
        }

        public void Send(byte[] buffer, TcpClient client)
        {
            if(client == null)
                return;
            var stream = client.GetStream();
            stream.Write(buffer, 0, buffer.Length);
            base.Send(buffer, client.Client.RemoteEndPoint as IPEndPoint);
        }

        public Task SendFile(string file, IPEndPoint ep, long offset, long? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            return SendFile(file, GetClient(ep), offset, end, preBuffer, postBuffer);
        }
        public async Task SendFile(string file, TcpClient client, long offset, long? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            if(client == null)
                return;
            FileStream fs = File.OpenRead(file);
            NetworkStream ns = client.GetStream();

            Task sendPreBufferTask = Task.CompletedTask;
            long bytesSent = 0;
            try{
                if(preBuffer != null)
                {
                    sendPreBufferTask = ns.WriteAsync(preBuffer, 0, preBuffer.Length);
                }
                            
                long fileSize = 0;
                if(end == null || end.Value < offset)
                    fileSize = new FileInfo(file).Length; 
                else
                    fileSize = end.Value - offset;
                var buffer = new byte[fileSize > bufferSize ? bufferSize : fileSize];
                long totalReadBytes = 0;
                
                fs.Position = offset;

                await sendPreBufferTask;
                bytesSent = preBuffer?.Length ?? 0; 
                do
                {
                    int readBytes = await fs.ReadAsync(buffer, 0, buffer.Length);
                    if(readBytes == 0)
                        break;
                    await ns.WriteAsync(buffer, 0, readBytes);
                    totalReadBytes += readBytes;
                    bytesSent += readBytes;
                }while(totalReadBytes < fileSize);
                    
                if(postBuffer != null)
                {
                    await ns.WriteAsync(postBuffer, 0, postBuffer.Length);
                    bytesSent += postBuffer.Length;
                }
            }catch(IOException){

            }
            finally
            {
                fs.Dispose(); 
                fs.Close(); 
                onSend?.Invoke(bytesSent, client.Client.RemoteEndPoint as IPEndPoint);
            }
        }
        public async Task SendFileToMultiple(string file, TcpClient[] clients, int offset, int? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            Task[] tasks = new Task[clients.Length];
            try
            {
                for(int i = 0; i < tasks.Length; ++i)
                {
                    tasks[i] = SendFile(file, clients[i], offset, end, preBuffer, postBuffer);
                }
                await Task.WhenAll(tasks);
            }
            catch (SocketException){}
        }
        public Task SendFileToMultiple(string file, IPEndPoint[] ep, int offset, int? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            TcpClient[] clients = new TcpClient[ep.Length];
            for(int i = 0; i < clients.Length; ++i)
                clients[i] = GetClient(ep[i]);
            return SendFileToMultiple(file, clients, offset, end, preBuffer, postBuffer);
        }
#endregion

#region Disconnect
        public void CloseClientSocket(TcpClient client)
        {
            try{
                client.Dispose();
                client.Close();

            }catch(Exception){} //Exception om socketen redan är stängd
            finally
            {
                if(client != null)
                {
                    //Interlocked.Decrement(ref connectedClients);
                    clients.Remove(client);
                    onClientClosed?.Invoke(client);
                }                
            }
        }

        public void StopListening()
        {
            listening = false;
            listener.Stop();
        }
        public override void Shutdown()
        {
            StopListening();
            foreach(var c in clients.ToArray())
            {
                CloseClientSocket(c);
            }
                
        }
#endregion

        public int ConnectedClients()
        {
            return connectedClients;
        }
        public TcpClient GetClient(IPEndPoint ep)
        {   
            if(ep == null)
                return null;
            
            var client = clients.Find(c => c?.Client?.RemoteEndPoint.Equals(ep) ?? false);
            return client;
        }
    }
}
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using TcpMethods;
using System.IO;

namespace Network
{
    public class ServerTcp : Server
    {
        private TcpListener listener;   
        public int connectedClients {get => clients.Count;}
        public int numWaitingClients {get => waitingClients.Count;}
        private List<TcpClient> clients = new List<TcpClient>();
        private Queue<TcpClient> waitingClients = new Queue<TcpClient>(); 
        public Action<IPEndPoint> onClientClosed;
        public Action clientAccepted;
        public bool listening {private set; get;}
        public int maxConnections;

#region Start
        
        public ServerTcp(int maxConnections, int bufferSize) : base(bufferSize, maxConnections)
        {
            this.maxConnections = maxConnections;
        }

        /// <summary>
        /// Start listening for incoming connections
        /// </summary>
        /// <param name="port">The port to listen on</param>
        /// <param name="err">Returns the error if the server failed to start, otherwise null</param>
        /// <returns>True if the server succesfully started listening for incoming connections, otherwise false</returns>
        public override bool StartListening(int port, out string err)
        {
            err = null;
            try{
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start(maxConnections);
            }
            catch(Exception e)
            {
                err = e.ToString();
                return false;
            }

            listening = true;
            Thread th = new Thread(StartAccept);
            th.IsBackground = true;
            th.Start(listener);
            return true;
        }
#endregion

#region Accept
        private void StartAccept(object tcpListener)
        {
            var listener = (TcpListener)tcpListener;
            try
            {
                while(true)
                {
                    if(listening && connectedClients+numWaitingClients < maxConnections)
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        //Accept(client);
                        ThreadPool.QueueUserWorkItem(Accept, client);
                    }
                }
            }
            catch (ObjectDisposedException){return;}
            catch(SocketException){return;}
        }

        private void Accept(object tcpClient)
        {
            //clients.Add(client); 
            //Interlocked.Increment(ref connectedClients);
            TcpClient client = (TcpClient)tcpClient;
            waitingClients.Enqueue(client);
            clientAccepted?.Invoke();
        }

        public bool FetchWaitingClient(out TcpClient client)
        {
            bool success = waitingClients.TryDequeue(out TcpClient c);
            client = c;
            if(success)
                clients.Add(client);
            return success;
        }
#endregion

#region Receive
        
        public Task<ReceiveResult> ReceiveAsync(IPEndPoint ep)
        {
            return Tcp.ReceiveAsync(GetClient(ep), bufferSize, new CancellationToken());
        }
        public Task<ReceiveResult> ReceiveAsync(IPEndPoint ep, CancellationToken token)
        {
            return Tcp.ReceiveAsync(GetClient(ep), bufferSize, token);
        }

        public async Task<ReceiveResult> ReceiveAsync(TcpClient client)
        {
            if(client == null)
                return ReceiveResult.Failed();
            return await Tcp.ReceiveAsync(client, bufferSize, new CancellationToken());
        }
        
#endregion

#region Send
        public override void Send(byte[] buffer, IPEndPoint ep)
        {
            var client = GetClient(ep);
            if(client == null)
                throw new NullReferenceException("No client with that IPEndPoint exists");
            Tcp.Send(buffer, client, onSend);
        }

        public void Send(byte[] buffer, TcpClient client)
        {
            if(client == null)
                throw new NullReferenceException("TcpClient is null");
            Tcp.Send(buffer, client, onSend);
        }



        public Task SendAsync(byte[] buffer, IPEndPoint ep)
        {
            var client = GetClient(ep);
            if (client == null)
                throw new NullReferenceException("No client with that IPEndPoint exists");
            return Tcp.SendAsync(buffer, client, onSend);
        }
        public Task SendAsync(byte[] buffer, TcpClient client)
        {
            
            if (client == null)
                throw new NullReferenceException("TcpClient is null");
            return Tcp.SendAsync(buffer, client, onSend);
        }

        public Task SendFileAsync(string file, IPEndPoint ep, long offset, long? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            return Tcp.SendFileAsync(file, GetClient(ep), bufferSize, onSend, offset, end, preBuffer, postBuffer);
        }
        public Task SendFileAsync(string file, TcpClient client, long offset, long? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            return Tcp.SendFileAsync(file, client, bufferSize, onSend, offset, end, preBuffer, postBuffer);
        }

        public async Task SendFileToMultipleAsync(string file, TcpClient[] clients, int offset, int? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            Task[] tasks = new Task[clients.Length];
            try
            {
                for(int i = 0; i < tasks.Length; ++i)
                {
                    tasks[i] = Tcp.SendFileAsync(file, clients[i], bufferSize, onSend, offset, end, preBuffer, postBuffer);
                }
                await Task.WhenAll(tasks);
            }
            catch (SocketException){}
        }
        public Task SendFileToMultipleAsync(string file, IPEndPoint[] ep, int offset, int? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            TcpClient[] clients = new TcpClient[ep.Length];
            for(int i = 0; i < clients.Length; ++i)
                clients[i] = GetClient(ep[i]);
            return SendFileToMultipleAsync(file, clients, offset, end, preBuffer, postBuffer);
        }
#endregion

#region Disconnect
        public void CloseClientSocket(TcpClient client)
        {
            IPEndPoint ep = null;
            try{
                ep = client?.Client?.RemoteEndPoint as IPEndPoint;
                client.Dispose();
                client.Close();

            }catch(NullReferenceException){} //Exception om socketen redan är stängd
            finally
            {
                if(client != null)
                {
                    //Interlocked.Decrement(ref connectedClients);
                    clients.Remove(client);
                    onClientClosed?.Invoke(ep);
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

       
        public TcpClient GetClient(IPEndPoint ep)
        {   
            if(ep == null)
                return null;
            
            var client = clients.Find(c => c?.Client?.RemoteEndPoint.Equals(ep) ?? false);
            return client;
        }
    }
}
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
        private Mutex closeMutex = new Mutex(false);
        private Mutex fetchMutex = new Mutex(false);
        private TcpListener listener;   
        public int connectedClients {get => clients.Count;}
        public int numWaitingClients {get => waitingClients.Count;}
        private List<ClientTcp> clients = new List<ClientTcp>();
        private Queue<ClientTcp> waitingClients = new Queue<ClientTcp>(); 
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
                        TcpClient c = listener.AcceptTcpClient();
                        ClientTcp client = new ClientTcp(bufferSize, c, true);
                        //Accept(client);
                        ThreadPool.QueueUserWorkItem(Accept, client);
                    }
                }
            }
            catch (ObjectDisposedException){return;}
            catch(SocketException){return;}
        }

        protected virtual void Accept(object clientTcp)
        {
            ClientTcp client = (ClientTcp)clientTcp;
            
            waitingClients.Enqueue(client);
            clientAccepted?.Invoke();
        }

        public bool FetchWaitingClient(out ClientTcp client, int timeout)
        {
            if(!fetchMutex.WaitOne(timeout))
            {
                client = null;
                return false;
            }

            bool success = waitingClients.TryDequeue(out ClientTcp c);
            client = c;
            if(success)
                clients.Add(client);

            fetchMutex.ReleaseMutex();
            return success;
        }
#endregion

#region Receive
        
        public Task<TcpReceiveResult> ReceiveAsync(IPEndPoint ep)
        {
            return Tcp.ReceiveAsync(GetClient(ep), bufferSize, new CancellationToken());
        }
        public Task<TcpReceiveResult> ReceiveAsync(IPEndPoint ep, CancellationToken token)
        {
            return Tcp.ReceiveAsync(GetClient(ep), bufferSize, token);
        }

        public async Task<TcpReceiveResult> ReceiveAsync(ClientTcp client)
        {
            if(client == null)
                return TcpReceiveResult.Failed(client);
            return await Tcp.ReceiveAsync(client, bufferSize, new CancellationToken());
        }
        
#endregion

#region Send
        public override void Send(byte[] buffer, IPEndPoint ep)
        {
            var client = GetClient(ep);
            if(client == null)
                throw new NullReferenceException("No client with that IPEndPoint exists");
            Send(buffer, client);
        }
        

        public void Send(byte[] buffer, ClientTcp client)
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
            return SendAsync(buffer, client);
        }
        public Task SendAsync(byte[] buffer, ClientTcp client)
        {
            
            if (client == null)
                throw new NullReferenceException("TcpClient is null");
            return Tcp.SendAsync(buffer, client, onSend);
        }

        public Task SendFileAsync(string file, IPEndPoint ep, long offset, long? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            var client = GetClient(ep);
            return SendFileAsync(file, client, offset, end, preBuffer, postBuffer);
        }
        public Task SendFileAsync(string file, ClientTcp client, long offset, long? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            return Tcp.SendFileAsync(file, client, bufferSize, offset, end, onSend, preBuffer, postBuffer);
        }

        public async Task SendFileToMultipleAsync(string file, ClientTcp[] clients, int offset, int? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            Task[] tasks = new Task[clients.Length];
            try
            {
                for(int i = 0; i < tasks.Length; ++i)
                {
                    tasks[i] = Tcp.SendFileAsync(file, clients[i], bufferSize, offset, end, onSend, preBuffer, postBuffer);
                }
                await Task.WhenAll(tasks);
            }
            catch (SocketException){}
        }
        public Task SendFileToMultipleAsync(string file, IPEndPoint[] ep, int offset, int? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            ClientTcp[] clients = new ClientTcp[ep.Length];
            for(int i = 0; i < clients.Length; ++i)
                clients[i] = GetClient(ep[i]);
            return SendFileToMultipleAsync(file, clients, offset, end, preBuffer, postBuffer);
        }

        public Task SendFileToAllAsync(string file, int offset, int? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            return SendFileToMultipleAsync(file, clients.ToArray(), offset, end, preBuffer, postBuffer);
        }
        #endregion

        #region Disconnect
        public void CloseClientSocket(ClientTcp client, int timeout)
        {
            if (!closeMutex.WaitOne(timeout))
                return;
            IPEndPoint ep = null;
            try{
                ep = client.client.Client.RemoteEndPoint as IPEndPoint;
                client.Shutdown();
            }catch(NullReferenceException){} 
            finally
            {
                if(client != null)
                {
                    clients.Remove(client);
                    closeMutex.ReleaseMutex();
              
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
                CloseClientSocket(c, -1);
            }
        }
#endregion

       
        public ClientTcp GetClient(IPEndPoint ep)
        {   
            if(ep == null)
                return null;
            
            var client = clients.Find(c => c?.client?.Client?.RemoteEndPoint.Equals(ep) ?? false);
            return client;
        }
    }
}
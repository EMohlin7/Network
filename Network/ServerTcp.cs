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

        public bool buffered;

#region Start
        
        public ServerTcp(int maxConnections, int bufferSize, bool buffered) : base(bufferSize, maxConnections)
        {
            this.maxConnections = maxConnections;
            this.buffered = buffered;
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
                        ClientTcp client = new ClientTcp(bufferSize, c, true, buffered);
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

        public bool FetchWaitingClient(out ClientTcp client, int millisecondsTimeout)
        {
            if(!fetchMutex.WaitOne(millisecondsTimeout))
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



        #endregion

        #region Send

        public override void Write(byte[] buffer, IPEndPoint ep)
        {
            ClientTcp client = GetClient(ep);
            client.Write(buffer);
            client.Flush();
        }


        public async Task WriteFileToMultipleAsync(string file, ClientTcp[] clients, int offset, int? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            Task[] tasks = new Task[clients.Length];
            try
            {
                for(int i = 0; i < tasks.Length; ++i)
                {
                    tasks[i] = clients[i].WriteFileAsync(file, offset, end, preBuffer, postBuffer);
                }
                await Task.WhenAll(tasks);
            }
            catch (SocketException){}
        }
        public Task WriteFileToMultipleAsync(string file, IPEndPoint[] ep, int offset, int? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            ClientTcp[] clients = new ClientTcp[ep.Length];
            for(int i = 0; i < clients.Length; ++i)
                clients[i] = GetClient(ep[i]);
            return WriteFileToMultipleAsync(file, clients, offset, end, preBuffer, postBuffer);
        }

        public Task WriteFileToAllAsync(string file, int offset, int? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            return WriteFileToMultipleAsync(file, clients.ToArray(), offset, end, preBuffer, postBuffer);
        }
        #endregion

        #region Disconnect
        public void CloseClientSocket(ClientTcp client, int millisecondsTimeout)
        {
            if (!closeMutex.WaitOne(millisecondsTimeout))
                return;
            IPEndPoint ep = null;
            try{
                ep = client.client.Client.RemoteEndPoint as IPEndPoint;
                client.Shutdown();
            }catch{} 
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
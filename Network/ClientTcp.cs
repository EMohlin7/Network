using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Network
{
    public class ClientTcp : Client
    {
        #region stream
        public class StreamHandler
        {
            //StreamWrapper is only used to invoke onSend when data is actually written to the stream and not just the buffer.
            class StreamWrapper : Stream
            {
                public Stream stream { private set; get; }
                private IPEndPoint remoteEndPoint;
                private ClientTcp tcp;
                public StreamWrapper(Stream s, IPEndPoint ep, ClientTcp tcp)
                {
                    stream = s;
                    this.tcp = tcp;
                    remoteEndPoint = ep;
                }

                public override bool CanRead => stream.CanRead;

                public override bool CanSeek => stream.CanSeek;

                public override bool CanWrite => stream.CanWrite;

                public override long Length => stream.Length;

                public override long Position { get => stream.Position; set => stream.Position = value; }

                public override void Flush()
                {
                    stream.Flush();
                }
                public override int Read(byte[] buffer, int offset, int count)
                {
                    return stream.Read(buffer, offset, count);
                }
                public override long Seek(long offset, SeekOrigin origin)
                {
                    return stream.Seek(offset, origin);
                }
                public override void SetLength(long value)
                {
                    stream.SetLength(value);
                }
                public override void Write(byte[] buffer, int offset, int count)
                {
                    stream.Write(buffer, offset, count);
                    tcp.onSend?.Invoke(count, remoteEndPoint);
                }
            }
            private ClientTcp tcp;
            private BufferedStream bufStream;
            private StreamWrapper internalStream;

            public StreamHandler(ClientTcp tcp, Stream stream)
            {
                this.tcp = tcp;
                SetStream(stream);
                tcp.bufSizeChanged += OnBufSizeChanged;
            }

            private void OnBufSizeChanged(int size)
            {
                bufStream = new BufferedStream(bufStream.UnderlyingStream, size);
            }
           
            public void SetStream(Stream stream)
            {
                internalStream = new StreamWrapper(stream, tcp.client.Client.RemoteEndPoint as IPEndPoint, tcp);
                bufStream = new BufferedStream(internalStream, tcp.bufferSize);
            }

            public void Dispose()
            {
                internalStream.Dispose();
                bufStream.Dispose();
            }

            public Stream GetStream() => tcp.buffered ? bufStream : internalStream;
           /*( public long GetStreamLength()
            {
                StreamWrapper sw = tcp.buffered ? bufStream.UnderlyingStream as StreamWrapper : internalStream;
                return sw.stream is SslStream ? ((SslStream)sw.stream).Length : sw.stream.Length();
            }*/
        }
        protected StreamHandler sh;

        public Stream stream
        {
            get => sh.GetStream();
            set
            {
                if(value == null)
                    throw new ArgumentNullException(nameof(value));
                if (sh == null)
                    sh = new StreamHandler(this, value);
                else
                    sh.SetStream(value);
            }
        }
        public bool buffered = false;
        #endregion


        public TcpClient client { private set; get; } = new TcpClient(AddressFamily.InterNetwork);



        public ClientTcp(int bufferSize, bool buffered) : base(bufferSize)
        {
            this.buffered = buffered;
        }
        public ClientTcp(int bufferSize, TcpClient client, bool connected, bool buffered) : base(bufferSize)
        {
            this.client = client;
            this.buffered = buffered;
            if (connected)
            {
                stream = client.GetStream();
                IPEndPoint ep = client.Client.RemoteEndPoint as IPEndPoint;
                SetConnectionInfo(new DnsEndPoint(ep.Address.ToString(), ep.Port), (client.Client.LocalEndPoint as IPEndPoint).Port);
            }
        }
        public ClientTcp(int bufferSize, TcpClient client, Stream stream, bool connected, bool buffered) : base(bufferSize)
        {
            this.client = client;
            this.stream = stream;
            this.buffered = buffered;
        
            if (connected)
            {
                IPEndPoint ep = client.Client.RemoteEndPoint as IPEndPoint;
                SetConnectionInfo(new DnsEndPoint(ep.Address.ToString(), ep.Port), (client.Client.LocalEndPoint as IPEndPoint).Port);
            }
        }

        public override async Task<bool> Connect(string host, int remotePort)
        {
            try
            {
                if (connected)
                    Shutdown();

                await client.ConnectAsync(host, remotePort);
                stream = client.GetStream();
                OnConnect(new DnsEndPoint(host, (client.Client.RemoteEndPoint as IPEndPoint).Port), (client.Client.LocalEndPoint as IPEndPoint).Port);
                return true;
            }
            catch (Exception)
            {
                Shutdown();
                return false;
            }
        }
        public override async Task<bool> Connect(string host, int remotePort, int localPort)
        {
            try
            {
                if (connected)
                    Shutdown();

                await client.ConnectAsync(host, remotePort);
                OnConnect(new DnsEndPoint(host, (client.Client.RemoteEndPoint as IPEndPoint).Port), localPort);
                return true;
            }
            catch (Exception)
            {
                Shutdown();
                return false;
            }
        }

        public override async Task<bool> Connect(IPAddress ip, int remotePort)
        {
            try
            {
                if (connected)
                    Shutdown();

                await client.ConnectAsync(ip, remotePort);
                OnConnect(new DnsEndPoint(ip.ToString(), (client.Client.RemoteEndPoint as IPEndPoint).Port), (client.Client.LocalEndPoint as IPEndPoint).Port);
                return true;
            }
            catch (Exception)
            {
                Shutdown();
                return false;
            }
        }

        public override async Task<bool> Connect(IPAddress ip, int remotePort, int localPort)
        {
            try
            {
                if (connected)
                    Shutdown();

                await client.ConnectAsync(ip, remotePort);
                OnConnect(new DnsEndPoint(ip.ToString(), (client.Client.RemoteEndPoint as IPEndPoint).Port), localPort);
                return true;
            }
            catch (Exception)
            {
                Shutdown();
                return false;
            }
        }

        public override void Write(byte[] buffer)
        {
            
            stream.Write(buffer, 0, buffer.Length);
            
        }
        public override async Task WriteAsync(byte[] buffer)
        {
            await stream.WriteAsync(buffer, 0, buffer.Length);
        }

        public async Task WriteFileAsync(string file, long offset, long? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            
            FileStream fs = File.OpenRead(file);
            Stream netStream = stream;

            Task sendPreBufferTask = Task.CompletedTask;
            long bytesSent = 0;
            try
            {
                if (preBuffer != null)
                {
                    sendPreBufferTask = netStream.WriteAsync(preBuffer, 0, preBuffer.Length);
                }

                long fileSize = 0;
                if (end == null || end.Value < offset)
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
                    if (readBytes == 0)
                        break;
                    await netStream.WriteAsync(buffer, 0, readBytes);
                    totalReadBytes += readBytes;
                    bytesSent += readBytes;
                } while (totalReadBytes < fileSize);

                if (postBuffer != null)
                {
                    await netStream.WriteAsync(postBuffer, 0, postBuffer.Length);
                    bytesSent += postBuffer.Length;
                }
            }
            catch (IOException)
            {

            }
            finally
            {
                fs.Dispose();
                fs.Close();
            }
        }
        public void WriteFile(string file, long offset, long? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {

            FileStream fs = File.OpenRead(file);
            Stream netStream = stream;

            long bytesSent = 0;
            try
            {
                if (preBuffer != null)
                {
                    netStream.Write(preBuffer, 0, preBuffer.Length);
                }

                long fileSize = 0;
                if (end == null || end.Value < offset)
                    fileSize = new FileInfo(file).Length;
                else
                    fileSize = end.Value - offset;
                var buffer = new byte[fileSize > bufferSize ? bufferSize : fileSize];
                long totalReadBytes = 0;

                fs.Position = offset;

                bytesSent = preBuffer?.Length ?? 0;
                do
                {
                    int readBytes = fs.Read(buffer, 0, buffer.Length);
                    if (readBytes == 0)
                        break;
                    netStream.Write(buffer, 0, readBytes);
                    totalReadBytes += readBytes;
                    bytesSent += readBytes;
                } while (totalReadBytes < fileSize);

                if (postBuffer != null)
                {
                    netStream.Write(postBuffer, 0, postBuffer.Length);
                    bytesSent += postBuffer.Length;
                }
            }
            catch (IOException)
            {

            }
            finally
            {
                fs.Dispose();
                fs.Close();
            }
        }

        public Task FlushAsync()
        {
            return stream.FlushAsync();
        }
        public void Flush()
        {
            try
            {
                stream.Flush();
            }
            catch (ObjectDisposedException) { }
        }


        public override ReceiveResult Receive()
        {
            byte[] buffer = new byte[bufferSize];
            try
            {

                var stream = this.stream;
                
                int bytes = stream.Read(buffer, 0, bufferSize);

                return new ReceiveResult(buffer, bytes, client.Client.RemoteEndPoint as IPEndPoint, SocketType.Stream, bytes >= bufferSize);
            }
            catch (Exception e) when (RecExFilter(e)) { return ReceiveResult.Failed(); }
        }
        public override async Task<ReceiveResult> ReceiveAsync()
        {
            byte[] buffer = new byte[bufferSize];
            try
            {

                var stream = this.stream;
                //do
                //{          

                int bytes = await stream.ReadAsync(buffer, 0, bufferSize);

                // }while(bytes == 0);
                byte[] result = new byte[bytes];
                Array.Copy(buffer, result, bytes);
                return new ReceiveResult(result, bytes, client.Client.RemoteEndPoint as IPEndPoint, SocketType.Stream, bytes >= bufferSize);
            }
            catch (Exception e) when (RecExFilter(e)) { return ReceiveResult.Failed(); }
        }
        private static bool RecExFilter(Exception e)
        {
            if (e is IOException || e is NullReferenceException || e is InvalidOperationException)
                return true;

            return false;
        }


        public override void Shutdown()
        {
            if (client != null)
            {
                try
                {
                    sh?.Dispose();
                }
                catch (InvalidOperationException) { }
                client.Close();
            }

            base.Shutdown();
        }
    }
}
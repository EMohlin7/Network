using Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TcpMethods
{
    internal static class Tcp
    {
        public static async Task<TcpReceiveResult> ReceiveAsync(ClientTcp client, int bufferSize, CancellationToken token)
        { 
            byte[] buffer = new byte[bufferSize];
            //int totalBytes = 0;
            int bytes = 0;
            
            //List<byte> receivedBytes = new List<byte>();
            try{
                var stream = client.GetStream();
                //do
                //{          
                bytes = await stream.ReadAsync(buffer, 0, bufferSize, token);
                    
               // }while(bytes == 0);
                var received = new byte[bytes];
                Array.Copy(buffer, received, bytes);
                //totalBytes += bytes;
                //receivedBytes.AddRange(buffer);
                
                
                return new TcpReceiveResult(received, bytes, client);
            }
            catch(Exception e ) when (ExceptionFilter(e)) {return TcpReceiveResult.Failed(client);}
        }
        private static bool ExceptionFilter(Exception e)
        {
            if (e is IOException || e is NullReferenceException || e is InvalidOperationException)
                return true;

            return false;
        }


        public static void Send(byte[] buffer, ClientTcp client, Action<long, IPEndPoint> onSend = null)
        {
            var stream = client.GetStream();
            stream.Write(buffer, 0, buffer.Length);
            onSend?.Invoke(buffer.LongLength, client.client.Client.RemoteEndPoint as IPEndPoint);
        }
        public static async Task SendAsync(byte[] buffer, ClientTcp client, Action<long, IPEndPoint> onSend = null)
        {
            await client.GetStream().WriteAsync(buffer, 0, buffer.Length);
            onSend?.Invoke(buffer.LongLength, client.client.Client.RemoteEndPoint as IPEndPoint);
        }


        public static async Task SendFileAsync(string file, ClientTcp client, int bufferSize, long offset, 
            long? end = null, Action<long, IPEndPoint> onSend = null, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            if(client == null)
                return;
            FileStream fs = File.OpenRead(file);
            Stream netStream = client.GetStream();

            Task sendPreBufferTask = Task.CompletedTask;
            long bytesSent = 0;
            try{
                if(preBuffer != null)
                {
                    sendPreBufferTask = netStream.WriteAsync(preBuffer, 0, preBuffer.Length);
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
                    await netStream.WriteAsync(buffer, 0, readBytes);
                    totalReadBytes += readBytes;
                    bytesSent += readBytes;
                }while(totalReadBytes < fileSize);
                    
                if(postBuffer != null)
                {
                    await netStream.WriteAsync(postBuffer, 0, postBuffer.Length);
                    bytesSent += postBuffer.Length;
                }
            }catch(IOException){

            }
            finally
            {
                fs.Dispose(); 
                fs.Close(); 
                onSend?.Invoke(bytesSent, client.client.Client.RemoteEndPoint as IPEndPoint);
            }
        }
    }
    
}
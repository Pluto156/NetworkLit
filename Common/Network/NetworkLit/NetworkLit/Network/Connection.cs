using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLit.Network
{
    public class Connection
    {
        private KCP kcp;
        private Action<MessageType, byte[]> Handle;

        public bool heartbeat;
        public IPEndPoint EndPoint
        {
            private set;
            get;
        }

        public Connection(IPEndPoint endPoint, Action<IPEndPoint, byte[], int> Send, Action<IPEndPoint, MessageType, byte[]> Handle)
        {
            this.EndPoint = endPoint;
            this.heartbeat = true;

            this.kcp = new KCP(1, (byte[] buffer, int size) => Send(this.EndPoint, buffer, size));
            this.kcp.NoDelay(1, 10, 2, 1);
            this.kcp.WndSize(128, 128);

            this.Handle = (MessageType id, byte[] data) => Handle(this.EndPoint, id, data);
        }

        public void Update(uint current)
        {
            this.kcp.Update(current);
            for (var size = this.kcp.PeekSize(); size > 0; size = this.kcp.PeekSize())
            {
                var buffer = new byte[size];
                Console.WriteLine($" this.kcp.Recv(buffer) {this.kcp.Recv(buffer)}  ");

                if (this.kcp.Recv(buffer) > 0)
                {
                    Console.WriteLine("kcp.Recv(buffer) ");
                    foreach (byte b in buffer)
                    {
                        Console.Write($"{b:X2} "); // 以十六进制格式打印，每个字节两位
                    }
                    Console.WriteLine("\n");
                    MessagePackage package = MessagePackage.Read(buffer);
                    if (package.Content==null)
                    {
                        package.Content = new byte[1];
                    }
                    this.Handle((MessageType)package.MessageId, package.Content);
                }
            }
        }

        public void Send(byte[] buffer)
        {
            Console.WriteLine($"kcp send {EndPoint.ToString()} ");
            foreach (byte b in buffer)
            {
                Console.Write($"{b:X2} "); // 以十六进制格式打印，每个字节两位
            }
            Console.WriteLine("\n");

            this.kcp.Send(buffer);
        }

        public void Input(byte[] buffer)
        {
            Console.WriteLine($"kcp input {EndPoint.ToString()}");
            foreach (byte b in buffer)
            {
                Console.Write($"{b:X2} "); // 以十六进制格式打印，每个字节两位
            }
            Console.WriteLine("\n");

            this.heartbeat = true;
            this.kcp.Input(buffer);
        }
    }
}

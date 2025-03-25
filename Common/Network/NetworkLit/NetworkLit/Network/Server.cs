using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace NetworkLit.Network
{
    public class Server : EndPort
    {
        private Dictionary<string, Connection> connectionMap;

        public int ConnectionCount
        {
            get
            {
                return this.connectionMap.Count;
            }
        }

        public Server() : base()
        {
            this.connectionMap = new Dictionary<string, Connection>();
            this.RegisterHandler(MessageType.Heartbeat, this.Heartbeat);
        }

        public bool Listen(int port)
        {
            if (base.Init())
            {
                try
                {
                    this.udp = new UdpClient(port);
                }
                catch
                {
                    this.Active = false;
                    return false;
                }

                this.Receive();

                return true;
            }

            return false;
        }

        public override void Update(float dt)
        {
            if (!this.Active)
            {
                return;
            }

            this.updateTime += dt;
            this.heartbeatTimer.Update(dt);
            var clock = this.ToKCPClock();

            foreach (var c in this.connectionMap)
            {
                c.Value.Update(clock);
            }
        }

        public void Send(IPEndPoint ep, MessagePackage package = null)
        {
            var fd = ep.ToString();

            if (this.connectionMap.ContainsKey(fd))
            {
                var connection = this.connectionMap[fd];
                base.Send(connection, package);
                Console.WriteLine($"Server send to {ep} ");

            }
        }

        public void SendToAll(MessagePackage message = null)
        {
            foreach (var c in this.connectionMap)
            {
                base.Send(c.Value, message);
            }
        }

        protected override void ReceiveCallback(IAsyncResult ar)
        {
            IPEndPoint ep = null;
            byte[] buffer = null;
            try
            {
                buffer = this.udp.EndReceive(ar, ref ep);
                Console.WriteLine($"Server recv  ip: {ep} size{buffer.Length} ");

            }
            catch (Exception ex) { }
            
            if (buffer != null && buffer.Length >= 26)
            {
                ushort msgId = BitConverter.ToUInt16(buffer, 24);  // 从下标 24 开始读取 2 个字节
                string fd = ep.ToString();

                if (!this.connectionMap.ContainsKey(fd) && (MessageType)msgId == MessageType.Connect)
                {
                    this.connectionMap.Add(fd, new Connection(ep, this.SendWrap, this.Handle));
                }

                if (this.connectionMap.ContainsKey(fd))
                {
                    this.connectionMap[fd].Input(buffer);
                }
            }

            this.Receive();
        }

        protected override void HeartbeatTick()
        {
            var removeList = new List<string>();

            foreach (var c in this.connectionMap)
            {
                if (!c.Value.heartbeat)
                {
                    removeList.Add(c.Key);
                }
                else
                {
                    c.Value.heartbeat = false;
                }
            }

            foreach (var k in removeList)
            {
                this.Handle(this.connectionMap[k].EndPoint, MessageType.Disconnect, null);
                this.connectionMap.Remove(k);
            }

            this.heartbeatTimer.Start();
        }

        private void Heartbeat(MessageType id, ByteBuffer reader, IPEndPoint ep)
        {
            this.Send(ep, MessagePackage.Build((ushort)MessageType.Heartbeat, null));
            Console.Write($"Server Heartbeat   {ep.ToString()}\n ");
        }
    }
}

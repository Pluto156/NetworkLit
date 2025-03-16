using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLit.Network
{
    public class Client : EndPort
    {
        private Connection connection;

        public bool Connect(string address, int port)
        {
            if (base.Init())
            {

                var ipEndPoint = new IPEndPoint(Dns.GetHostAddresses(address)[0], port);
                this.udp = new UdpClient(8889);

                this.connection = new Connection(ipEndPoint, this.SendWrap, this.Handle);

                this.Send(MessagePackage.Build((ushort)MessageType.Connect, new Byte[] { 1,2,3}));
                this.Receive();

                return true;
            }

            return false;
        }

        public override bool Close()
        {
            if (base.Close())
            {
                this.Handle(null, MessageType.Disconnect, null);

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
            this.connection.Update(this.ToKCPClock());
        }

        public void Send(MessagePackage message = null)
        {
            base.Send(this.connection, message);
        }

        protected override void SendWrap(IPEndPoint ep, byte[] buffer, int size)
        {
            try
            {
                base.SendWrap(ep, buffer, size);
                Console.WriteLine("client SendWrap ");
                for (int i=0;i<size;++i)
                {
                    Console.Write($"{buffer[i]:X2} "); // 以十六进制格式打印，每个字节两位
                }
                Console.WriteLine("\n");
            }
            catch (SocketException)
            {
                this.Close();
            }
        }

        protected override void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                IPEndPoint ep = null;
                var buffer = this.udp.EndReceive(ar, ref ep);

                if (buffer != null)
                {
                    this.connection.Input(buffer);
                }

                this.Receive();
            }
            catch (SocketException)
            {
                this.Close();
            }
        }

        protected override void HeartbeatTick()
        {
            if (!this.connection.heartbeat)
            {
                this.Close();
            }
            else
            {
                this.Send(MessagePackage.Build((ushort)MessageType.Heartbeat, null));
                this.connection.heartbeat = false;
                this.heartbeatTimer.Start();
            }
        }
    }
}

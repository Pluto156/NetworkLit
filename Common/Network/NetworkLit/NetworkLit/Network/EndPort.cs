using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NetworkLit.Utility;
using Timer = NetworkLit.Utility.Timer;

namespace NetworkLit.Network
{ 
    public abstract class EndPort
    {
        private const float HEARTBEAT_INTERVAL = 3;

        protected UdpClient udp;
        protected float updateTime;
        protected Timer heartbeatTimer;
        private Dictionary<MessageType, Action<MessageType, ByteBuffer, IPEndPoint>> handlerMap;

        public bool Active
        {
            get;
            protected set;
        }

        public EndPort()
        {
            this.handlerMap = new Dictionary<MessageType, Action<MessageType, ByteBuffer, IPEndPoint>>();
            this.heartbeatTimer = new Timer(HEARTBEAT_INTERVAL, this.HeartbeatTick);
        }

        protected bool Init()
        {
            if (this.Active)
            {
                return false;
            }

            this.updateTime = 0;
            this.Active = true;
            this.heartbeatTimer.Start();

            return true;
        }

        public virtual bool Close()
        {
            if (!this.Active)
            {
                return false;
            }

            this.Active = false;
            this.udp.Close();

            return true;
        }

        public abstract void Update(float dt);

        public void RegisterHandler(MessageType id, Action<MessageType, ByteBuffer, IPEndPoint> Func)
        {
            Console.WriteLine("RegisterHandler " + id+" "+GetHashCode());
            this.handlerMap.Add(id, Func);
        }

        protected uint ToKCPClock()
        {
            return (uint)Math.Floor(this.updateTime * 1000);
        }

        protected void Handle(IPEndPoint ep, MessageType id, byte[] data)
        {
            if(data == null)data = new byte[0];
            Console.WriteLine("Handle " + id + " " + GetHashCode() +" " +this.handlerMap.Count);

            var reader = new ByteBuffer(data);


            if (this.handlerMap.ContainsKey(id))
            {
                this.handlerMap[id](id, reader, ep);
            }
        }

        protected void Send(Connection connection, MessagePackage package = null)
        {
            byte[] buffer;

            if (package != null)
            {
                buffer = MessagePackage.Write(package);
            }
            else
            {
                buffer = new byte[] { };
            }

            connection.Send(buffer);
        }

        protected void Receive()
        {
            this.udp.BeginReceive(this.ReceiveCallback, null);
        }

        protected virtual void SendWrap(IPEndPoint ep, byte[] buffer, int size)
        {
            this.udp.BeginSend(buffer, size, ep, this.SendCallback, null);
        }

        protected abstract void ReceiveCallback(IAsyncResult ar);

        protected void SendCallback(IAsyncResult ar)
        {
            this.udp.EndSend(ar);
        }

        protected abstract void HeartbeatTick();
    }
}

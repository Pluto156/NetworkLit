using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLit.Network
{
    public enum MessageType : ushort
    {
        Connect,
        Disconnect,
        Heartbeat,
    }
}

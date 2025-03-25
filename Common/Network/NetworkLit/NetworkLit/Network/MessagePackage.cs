using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetworkLit.Utility;

namespace NetworkLit.Network
{
    public class MessagePackage : IDisposable
    {
        public ushort MessageId;
        public bool HasContent { private set; get; }
        byte[] m_Content;

        public byte[] Content
        {
            set
            {
                if (value != null)
                {
                    m_Content = value;
                    HasContent = true;
                }
            }
            get
            {
                return m_Content;
            }
        }

        public object ExtraObj = null;
        public static MessagePackage Build(ushort messageId)
        {
            MessagePackage package = new MessagePackage();
            package.MessageId = messageId;
            return package;
        }
        public static MessagePackage Build(ushort messageId, byte[] content)
        {
            MessagePackage package = Build(messageId);
            package.Content = content;
            return package;
        }
        public static MessagePackage BuildParams(ushort messageId, params object[] pars)
        {
            using (ByteBuffer buffer = new ByteBuffer())
            {
                foreach (object i in pars)
                {
                    Type iType = i.GetType();
                    if (iType == typeof(int))
                    {
                        buffer.WriteInt32((int)i);
                    }
                    else if (iType == typeof(uint))
                    {
                        buffer.WriteUInt32((uint)i);
                    }
                    else if (iType == typeof(float))
                    {
                        buffer.WriteFloat((float)i);
                    }
                    else if (iType == typeof(bool))
                    {
                        buffer.WriteBool((bool)i);
                    }
                    else if (iType == typeof(long))
                    {
                        buffer.WriteInt64((long)i);
                    }
                    else if (iType == typeof(ulong))
                    {
                        buffer.WriteUInt64((ulong)i);
                    }
                    else if (iType == typeof(short))
                    {
                        buffer.WriteInt16((short)i);
                    }
                    else if (iType == typeof(ushort))
                    {
                        buffer.WriteUInt16((ushort)i);
                    }
                    else if (iType == typeof(byte))
                    {
                        buffer.WriteByte((byte)i);
                    }
                    else if (iType == typeof(string))
                    {
                        buffer.WriteString((string)i);
                    }
                    else if (iType == typeof(byte[]))
                    {
                        buffer.WriteBytes((byte[])i);
                    }
                    else
                    {
                        throw new Exception("BuildParams Type is not supported. " + iType.ToString());
                    }
                }
                return Build(messageId, buffer.Getbuffer());
            }
        }
        public static MessagePackage Read(byte[] bytes)
        {
            using (ByteBuffer buffer = new ByteBuffer(bytes))
            {
                MessagePackage info = new MessagePackage();
                info.MessageId = buffer.ReadUInt16();
                info.HasContent = buffer.ReadBool();
                if (info.HasContent)
                {
                    info.Content = PackageCompress.Decompress(buffer.ReadBytes());
                }
                return info;
            }
        }
        public static byte[] Write(MessagePackage info)
        {
            using (ByteBuffer buffer = new ByteBuffer())
            {
                buffer.WriteUInt16(info.MessageId);
                buffer.WriteBool(info.HasContent);
                if (info.HasContent)
                {
                    buffer.WriteBytes(PackageCompress.Compress(info.Content));
                }
                return buffer.Getbuffer();
            }
        }
        public void Dispose()
        {
            m_Content = null;
            MessageId = 0;
            HasContent = false;
            ExtraObj = null;
        }
    }
}

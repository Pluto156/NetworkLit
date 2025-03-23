using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace NetworkLit.Network
{
    public class KCP
    {
        /// <summary>
        /// no delay最小重传超时
        /// </summary>
        public const int IKCP_RTO_NDL = 30;  // no delay min rto
        /// <summary>
        /// delay最小重传超时
        /// </summary>
        public const int IKCP_RTO_MIN = 100; // normal min rto
        /// <summary>
        /// 默认最小重传超时
        /// </summary>
        public const int IKCP_RTO_DEF = 200;
        /// <summary>
        /// 最大重传超时
        /// </summary>
        public const int IKCP_RTO_MAX = 60000;
        /// <summary>
        /// 数据推送命令
        /// </summary>
        public const int IKCP_CMD_PUSH = 81; // cmd: push data
        /// <summary>
        /// 确认命令
        /// </summary>
        public const int IKCP_CMD_ACK = 82; // cmd: ack
        /// <summary>
        /// IKCP_CMD_WASK 接收窗口大小询问命令
        /// </summary>
        public const int IKCP_CMD_WASK = 83; // cmd: window probe (ask)
        public const int IKCP_CMD_WINS = 84; // cmd: window size (tell)
        /// <summary>
        /// 表示需要发送探测请求。IKCP_ASK_SEND 是一个用于发送探测请求的命令
        /// </summary>
        public const int IKCP_ASK_SEND = 1;  // need to send IKCP_CMD_WASK
        public const int IKCP_ASK_TELL = 2;  // need to send IKCP_CMD_WINS
        public const int IKCP_WND_SND = 32;
        public const int IKCP_WND_RCV = 32;
        public const int IKCP_MTU_DEF = 1400;
        public const int IKCP_ACK_FAST = 3;
        public const int IKCP_INTERVAL = 100;
        public const int IKCP_OVERHEAD = 24;
        /// <summary>
        /// 数据段的发送次数超过了 dead_link 的阈值，说明该数据段可能由于网络问题无法传送，设置 state = 0，表示此时连接可能出现问题或失效
        /// </summary>
        public const int IKCP_DEADLINK = 10;
        public const int IKCP_THRESH_INIT = 2;
        /// <summary>
        /// 慢启动最小阈值
        /// </summary>
        public const int IKCP_THRESH_MIN = 2;
        public const int IKCP_PROBE_INIT = 7000;   // 7 secs to probe window size
        public const int IKCP_PROBE_LIMIT = 120000; // up to 120 secs to probe window


        // encode 8 bits unsigned int
        public static int ikcp_encode8u(byte[] p, int offset, byte c)
        {
            p[0 + offset] = c;
            return 1;
        }

        // decode 8 bits unsigned int
        public static int ikcp_decode8u(byte[] p, int offset, ref byte c)
        {
            c = p[0 + offset];
            return 1;
        }

        /* encode 16 bits unsigned int (lsb) */
        public static int ikcp_encode16u(byte[] p, int offset, UInt16 w)
        {
            p[0 + offset] = (byte)(w >> 0);
            p[1 + offset] = (byte)(w >> 8);
            return 2;
        }

        /* decode 16 bits unsigned int (lsb) */
        public static int ikcp_decode16u(byte[] p, int offset, ref UInt16 c)
        {
            UInt16 result = 0;
            result |= (UInt16)p[0 + offset];
            result |= (UInt16)(p[1 + offset] << 8);
            c = result;
            return 2;
        }

        /* encode 32 bits unsigned int (lsb) */
        public static int ikcp_encode32u(byte[] p, int offset, UInt32 l)
        {
            p[0 + offset] = (byte)(l >> 0);
            p[1 + offset] = (byte)(l >> 8);
            p[2 + offset] = (byte)(l >> 16);
            p[3 + offset] = (byte)(l >> 24);
            return 4;
        }

        /* decode 32 bits unsigned int (lsb) */
        public static int ikcp_decode32u(byte[] p, int offset, ref UInt32 c)
        {
            UInt32 result = 0;
            result |= (UInt32)p[0 + offset];
            result |= (UInt32)(p[1 + offset] << 8);
            result |= (UInt32)(p[2 + offset] << 16);
            result |= (UInt32)(p[3 + offset] << 24);
            c = result;
            return 4;
        }

        public static byte[] slice(byte[] p, int start, int stop)
        {
            var bytes = new byte[stop - start];
            Array.Copy(p, start, bytes, 0, bytes.Length);
            return bytes;
        }

        public static T[] slice<T>(T[] p, int start, int stop)
        {
            var arr = new T[stop - start];
            var index = 0;
            for (var i = start; i < stop; i++)
            {
                arr[index] = p[i];
                index++;
            }

            return arr;
        }

        public static byte[] append(byte[] p, byte c)
        {
            var bytes = new byte[p.Length + 1];
            Array.Copy(p, bytes, p.Length);
            bytes[p.Length] = c;
            return bytes;
        }

        public static T[] append<T>(T[] p, T c)
        {
            var arr = new T[p.Length + 1];
            for (var i = 0; i < p.Length; i++)
                arr[i] = p[i];
            arr[p.Length] = c;
            return arr;
        }

        public static T[] append<T>(T[] p, T[] cs)
        {
            var arr = new T[p.Length + cs.Length];
            for (var i = 0; i < p.Length; i++)
                arr[i] = p[i];
            for (var i = 0; i < cs.Length; i++)
                arr[p.Length + i] = cs[i];
            return arr;
        }

        static UInt32 _imin_(UInt32 a, UInt32 b)
        {
            return a <= b ? a : b;
        }

        static UInt32 _imax_(UInt32 a, UInt32 b)
        {
            return a >= b ? a : b;
        }

        static UInt32 _ibound_(UInt32 lower, UInt32 middle, UInt32 upper)
        {
            return _imin_(_imax_(lower, middle), upper);
        }

        static Int32 _itimediff(UInt32 later, UInt32 earlier)
        {
            return ((Int32)(later - earlier));
        }

        // KCP Segment Definition
        internal class Segment
        {
            /// <summary>
            /// conv：连接号。UDP是无连接的，conv用于表示来自哪个客户端。对连接的一种替代，因为有conv，因此KCP也是支持多路复用
            /// </summary>
            internal UInt32 conv = 0;
            /// <summary>
            /// 命令类型 IKCP_CMD_PUSH:数据推送命令 IKCP_CMD_ACK：确认命令 IKCP_CMD_WASK 接收窗口大小询问命令 IKCP_CMD_WINS 接收窗口大小告知命令 
            /// </summary>
            internal UInt32 cmd = 0;
            /// <summary>
            /// frg：分片，用户数据可能会被分成多个KCP包，发送出去 在send函数中其含义设置为 表示当前数据段还有多少个未发送的剩余数据段
            /// </summary>
            internal UInt32 frg = 0;
            /// <summary>
            /// wnd：接收窗口大小，发送方的发送窗口不能超过接收方给出的数值, （其实是接收窗口的剩余大小，这个大小是动态变化的)
            /// </summary>
            internal UInt32 wnd = 0;
            /// <summary>
            /// ts：时间序列
            /// </summary>
            internal UInt32 ts = 0;
            /// <summary>
            /// sn：序列号
            /// </summary>
            internal UInt32 sn = 0;
            /// <summary>
            /// una：下一个可接收的序列号。其实就是确认号，收到sn=10的包，una为11
            /// </summary>
            internal UInt32 una = 0;
            /// <summary>
            /// 数据段的重传时间戳
            /// </summary>
            internal UInt32 resendts = 0;
            /// <summary>
            /// 重传超时时间
            /// </summary>
            internal UInt32 rto = 0;
            /// <summary>
            /// 当前数据段的 快速确认（fastack） 次数
            /// </summary>
            internal UInt32 fastack = 0;
            /// <summary>
            /// 该数据段的发送次数
            /// </summary>
            internal UInt32 xmit = 0;
            internal byte[] data;

            internal Segment(int size)
            {
                this.data = new byte[size];
            }

            // encode a segment into buffer
            internal int encode(byte[] ptr, int offset)
            {

                var offset_ = offset;

                offset += ikcp_encode32u(ptr, offset, conv);
                offset += ikcp_encode8u(ptr, offset, (byte)cmd);
                offset += ikcp_encode8u(ptr, offset, (byte)frg);
                offset += ikcp_encode16u(ptr, offset, (UInt16)wnd);
                offset += ikcp_encode32u(ptr, offset, ts);
                offset += ikcp_encode32u(ptr, offset, sn);
                offset += ikcp_encode32u(ptr, offset, una);
                offset += ikcp_encode32u(ptr, offset, (UInt32)data.Length);

                return offset - offset_;
            }
        }

        // kcp members.
        UInt32 conv; 
        UInt32 mtu; 
        UInt32 mss; 
        UInt32 state;
        /// <summary>
        /// 是尚未确认的序列号（即已经发送但还没有被接收方确认的最小序列号）
        /// </summary>
        UInt32 snd_una;
        /// <summary>
        /// 是下一个待发送的数据段的序列号
        /// </summary>
        UInt32 snd_nxt; 
        UInt32 rcv_nxt;
        UInt32 ts_recent; 
        UInt32 ts_lastack;
        /// <summary>
        /// 慢启动阈值
        /// </summary>
        UInt32 ssthresh;
        UInt32 rx_rttval;
        UInt32 rx_srtt;
        /// <summary>
        /// 重传超时
        /// </summary>
        UInt32 rx_rto;
        /// <summary>
        /// 最小重传超时
        /// </summary>
        UInt32 rx_minrto;
        UInt32 snd_wnd; 
        UInt32 rcv_wnd; 
        UInt32 rmt_wnd;
        /// <summary>
        /// 拥塞窗口大小
        /// </summary>
        UInt32 cwnd; 
        UInt32 probe;
        UInt32 current;
        /// <summary>
        /// 设置内部定时器的更新间隔，单位是毫秒。默认值为 100ms。此值决定了KCP协议在每次更新时的周期。
        /// </summary>
        UInt32 interval; 
        UInt32 ts_flush;
        /// <summary>
        /// 总共已经发送的重传次数
        /// </summary>
        UInt32 xmit;
        UInt32 nodelay; 
        UInt32 updated;
        /// <summary>
        /// 
        /// </summary>
        UInt32 ts_probe; 
        UInt32 probe_wait;
        /// <summary>
        /// 数据段的发送次数超过了 dead_link 的阈值，说明该数据段可能由于网络问题无法传送，设置 state = 0，表示此时连接可能出现问题或失效
        /// </summary>
        UInt32 dead_link;
        /// <summary>
        /// 拥塞窗口的 增量（incr），即下一次能够发送的最大数据量，等于 当前的 cwnd 乘以 最大报文段大小（mss）
        /// </summary>
        UInt32 incr;

        Segment[] snd_queue = new Segment[0];
        Segment[] rcv_queue = new Segment[0];
        Segment[] snd_buf = new Segment[0];
        Segment[] rcv_buf = new Segment[0];

        UInt32[] acklist = new UInt32[0];

        byte[] buffer;
        /// <summary>
        /// 如果传入的值大于或等于 0，则将 fastresend 设置为传入值。fastresend = 0：禁用快速重传。fastresend = 1：启用快速重传。是快速重传的阈值，表示 当一个数据包在接收方被跳过（即被后续数据包多次 ACK 确认）达到一定次数时，就进行快速重传。
        /// </summary>
        Int32 fastresend;
        /// <summary>
        /// 用于启用或禁用KCP协议的拥塞控制。如果传入值为 1，则禁用拥塞控制，允许更高的发送速率；如果为 0（默认值），则启用拥塞控制。nocwnd = 1：禁用拥塞控制。nocwnd = 0：启用拥塞控制。
        /// </summary>
        Int32 nocwnd;
        Int32 logmask;
        // buffer, size
        Action<byte[], int> output;

        // create a new kcp control object, 'conv' must equal in two endpoint
        // from the same connection.
        public KCP(UInt32 conv_, Action<byte[], int> output_)
        {
            conv = conv_;
            snd_wnd = IKCP_WND_SND;
            rcv_wnd = IKCP_WND_RCV;
            rmt_wnd = IKCP_WND_RCV;
            mtu = IKCP_MTU_DEF;
            mss = mtu - IKCP_OVERHEAD;

            rx_rto = IKCP_RTO_DEF;
            rx_minrto = IKCP_RTO_MIN;
            interval = IKCP_INTERVAL;
            ts_flush = IKCP_INTERVAL;
            ssthresh = IKCP_THRESH_INIT;
            dead_link = IKCP_DEADLINK;
            buffer = new byte[(mtu + IKCP_OVERHEAD) * 3];
            output = output_;
        }

        // check the size of next message in the recv queue
        public int PeekSize()
        {
            if (0 == rcv_queue.Length) return -1;

            var seq = rcv_queue[0];

            if (0 == seq.frg) return seq.data.Length;

            if (rcv_queue.Length < seq.frg + 1) return -1;

            int length = 0;

            foreach (var item in rcv_queue)
            {
                length += item.data.Length;
                if (0 == item.frg)
                    break;
            }

            return length;
        }

        // user/upper level recv: returns size, returns below zero for EAGAIN
        public int Recv(byte[] buffer)
        {

            if (0 == rcv_queue.Length) return -1;

            var peekSize = PeekSize();
            if (0 > peekSize) return -2;

            if (peekSize > buffer.Length) return -3;

            var fast_recover = false;
            if (rcv_queue.Length >= rcv_wnd) fast_recover = true;

            // merge fragment.
            var count = 0;
            var n = 0;
            foreach (var seg in rcv_queue)
            {
                Array.Copy(seg.data, 0, buffer, n, seg.data.Length);
                n += seg.data.Length;
                count++;
                if (0 == seg.frg) break;
            }

            if (0 < count)
            {
                rcv_queue = slice<Segment>(rcv_queue, count, rcv_queue.Length);
            }

            // move available data from rcv_buf -> rcv_queue
            count = 0;
            foreach (var seg in rcv_buf)
            {
                if (seg.sn == rcv_nxt && rcv_queue.Length < rcv_wnd)
                {
                    rcv_queue = append<Segment>(rcv_queue, seg);
                    rcv_nxt++;
                    count++;
                }
                else
                {
                    break;
                }
            }

            if (0 < count) rcv_buf = slice<Segment>(rcv_buf, count, rcv_buf.Length);

            // fast recover
            if (rcv_queue.Length < rcv_wnd && fast_recover)
            {
                // ready to send back IKCP_CMD_WINS in ikcp_flush
                // tell remote my window size
                probe |= IKCP_ASK_TELL;
            }

            return n;
        }

        // user/upper level send, returns below zero for error
        public int Send(byte[] buffer)
        {

            if (0 == buffer.Length) return -1;

            var count = 0;

            if (buffer.Length < mss)
                count = 1;
            else
                count = (int)(buffer.Length + mss - 1) / (int)mss;

            if (255 < count) return -2;

            if (0 == count) count = 1;

            var offset = 0;

            for (var i = 0; i < count; i++)
            {
                var size = 0;
                if (buffer.Length - offset > mss)
                    size = (int)mss;
                else
                    size = buffer.Length - offset;

                var seg = new Segment(size);
                Array.Copy(buffer, offset, seg.data, 0, size);
                offset += size;
                seg.frg = (UInt32)(count - i - 1);
                snd_queue = append<Segment>(snd_queue, seg);
            }

            return 0;
        }

        // update ack.
        void update_ack(Int32 rtt)
        {
            if (0 == rx_srtt)
            {
                rx_srtt = (UInt32)rtt;
                rx_rttval = (UInt32)rtt / 2;
            }
            else
            {
                Int32 delta = (Int32)((UInt32)rtt - rx_srtt);
                if (0 > delta) delta = -delta;

                rx_rttval = (3 * rx_rttval + (uint)delta) / 4;
                rx_srtt = (UInt32)((7 * rx_srtt + rtt) / 8);
                if (rx_srtt < 1) rx_srtt = 1;
            }

            var rto = (int)(rx_srtt + _imax_(1, 4 * rx_rttval));
            rx_rto = _ibound_(rx_minrto, (UInt32)rto, IKCP_RTO_MAX);
        }

        void shrink_buf()
        {
            if (snd_buf.Length > 0)
                snd_una = snd_buf[0].sn;
            else
                snd_una = snd_nxt;
        }

        void parse_ack(UInt32 sn)
        {

            if (_itimediff(sn, snd_una) < 0 || _itimediff(sn, snd_nxt) >= 0) return;

            var index = 0;
            foreach (var seg in snd_buf)
            {
                if (sn == seg.sn)
                {
                    snd_buf = append<Segment>(slice<Segment>(snd_buf, 0, index), slice<Segment>(snd_buf, index + 1, snd_buf.Length));
                    break;
                }
                else
                {
                    seg.fastack++;
                }

                index++;
            }
        }

        void parse_una(UInt32 una)
        {
            var count = 0;
            foreach (var seg in snd_buf)
            {
                if (_itimediff(una, seg.sn) > 0)
                    count++;
                else
                    break;
            }

            if (0 < count) snd_buf = slice<Segment>(snd_buf, count, snd_buf.Length);
        }

        void ack_push(UInt32 sn, UInt32 ts)
        {
            acklist = append<UInt32>(acklist, new UInt32[2] { sn, ts });
        }

        void ack_get(int p, ref UInt32 sn, ref UInt32 ts)
        {
            sn = acklist[p * 2 + 0];
            ts = acklist[p * 2 + 1];
        }

        void parse_data(Segment newseg)
        {
            var sn = newseg.sn;
            if (_itimediff(sn, rcv_nxt + rcv_wnd) >= 0 || _itimediff(sn, rcv_nxt) < 0) return;

            var n = rcv_buf.Length - 1;
            var after_idx = -1;
            var repeat = false;
            for (var i = n; i >= 0; i--)
            {
                var seg = rcv_buf[i];
                if (seg.sn == sn)
                {
                    repeat = true;
                    break;
                }

                if (_itimediff(sn, seg.sn) > 0)
                {
                    after_idx = i;
                    break;
                }
            }

            if (!repeat)
            {
                if (after_idx == -1)
                    rcv_buf = append<Segment>(new Segment[1] { newseg }, rcv_buf);
                else
                    rcv_buf = append<Segment>(slice<Segment>(rcv_buf, 0, after_idx + 1), append<Segment>(new Segment[1] { newseg }, slice<Segment>(rcv_buf, after_idx + 1, rcv_buf.Length)));
            }

            // move available data from rcv_buf -> rcv_queue
            var count = 0;
            foreach (var seg in rcv_buf)
            {
                if (seg.sn == rcv_nxt && rcv_queue.Length < rcv_wnd)
                {
                    rcv_queue = append<Segment>(rcv_queue, seg);
                    rcv_nxt++;
                    count++;
                }
                else
                {
                    break;
                }
            }

            if (0 < count)
            {
                rcv_buf = slice<Segment>(rcv_buf, count, rcv_buf.Length);
            }
        }

        // when you received a low level packet (eg. UDP packet), call it
        public int Input(byte[] data)
        {

            var s_una = snd_una;
            if (data.Length < IKCP_OVERHEAD) return 0;

            var offset = 0;

            while (true)
            {
                UInt32 ts = 0;
                UInt32 sn = 0;
                UInt32 length = 0;
                UInt32 una = 0;
                UInt32 conv_ = 0;

                UInt16 wnd = 0;

                byte cmd = 0;
                byte frg = 0;

                if (data.Length - offset < IKCP_OVERHEAD) break;

                offset += ikcp_decode32u(data, offset, ref conv_);

                if (conv != conv_) return -1;

                offset += ikcp_decode8u(data, offset, ref cmd);
                offset += ikcp_decode8u(data, offset, ref frg);
                offset += ikcp_decode16u(data, offset, ref wnd);
                offset += ikcp_decode32u(data, offset, ref ts);
                offset += ikcp_decode32u(data, offset, ref sn);
                offset += ikcp_decode32u(data, offset, ref una);
                offset += ikcp_decode32u(data, offset, ref length);

                if (data.Length - offset < length) return -2;

                switch (cmd)
                {
                    case IKCP_CMD_PUSH:
                    case IKCP_CMD_ACK:
                    case IKCP_CMD_WASK:
                    case IKCP_CMD_WINS:
                        break;
                    default:
                        return -3;
                }

                rmt_wnd = (UInt32)wnd;
                parse_una(una);
                shrink_buf();

                if (IKCP_CMD_ACK == cmd)
                {
                    if (_itimediff(current, ts) >= 0)
                    {
                        update_ack(_itimediff(current, ts));
                    }
                    parse_ack(sn);
                    shrink_buf();
                }
                else if (IKCP_CMD_PUSH == cmd)
                {
                    if (_itimediff(sn, rcv_nxt + rcv_wnd) < 0)
                    {
                        ack_push(sn, ts);
                        if (_itimediff(sn, rcv_nxt) >= 0)
                        {
                            var seg = new Segment((int)length);
                            seg.conv = conv_;
                            seg.cmd = (UInt32)cmd;
                            seg.frg = (UInt32)frg;
                            seg.wnd = (UInt32)wnd;
                            seg.ts = ts;
                            seg.sn = sn;
                            seg.una = una;

                            if (length > 0) Array.Copy(data, offset, seg.data, 0, length);

                            parse_data(seg);
                        }
                    }
                }
                else if (IKCP_CMD_WASK == cmd)
                {
                    // ready to send back IKCP_CMD_WINS in Ikcp_flush
                    // tell remote my window size
                    probe |= IKCP_ASK_TELL;
                }
                else if (IKCP_CMD_WINS == cmd)
                {
                    // do nothing
                }
                else
                {
                    return -3;
                }

                offset += (int)length;
            }

            if (_itimediff(snd_una, s_una) > 0)
            {
                if (cwnd < rmt_wnd)
                {
                    var mss_ = mss;
                    if (cwnd < ssthresh)
                    {
                        cwnd++;
                        incr += mss_;
                    }
                    else
                    {
                        if (incr < mss_)
                        {
                            incr = mss_;
                        }
                        incr += (mss_ * mss_) / incr + (mss_ / 16);
                        if ((cwnd + 1) * mss_ <= incr) cwnd++;
                    }
                    if (cwnd > rmt_wnd)
                    {
                        cwnd = rmt_wnd;
                        incr = rmt_wnd * mss_;
                    }
                }
            }

            return 0;
        }

        Int32 wnd_unused()
        {
            if (rcv_queue.Length < rcv_wnd)
                return (Int32)(int)rcv_wnd - rcv_queue.Length;
            return 0;
        }

        // flush pending data
        void flush()
        {
            var current_ = current;
            var buffer_ = buffer;
            //记录进行了快速重传的次数
            var change = 0;
            //标记此次为是否为丢包重传
            var lost = 0;

            if (0 == updated) return;

            var seg = new Segment(0);
            seg.conv = conv;
            seg.cmd = IKCP_CMD_ACK;
            seg.wnd = (UInt32)wnd_unused();
            seg.una = rcv_nxt;

            // flush acknowledges
            var count = acklist.Length / 2;
            var offset = 0;
            for (var i = 0; i < count; i++)
            {
                if (offset + IKCP_OVERHEAD > mtu)
                {
                    output(buffer, offset);
                    //Array.Clear(buffer, 0, offset);
                    offset = 0;
                }
                ack_get(i, ref seg.sn, ref seg.ts);
                offset += seg.encode(buffer, offset);
            }
            acklist = new UInt32[0];

            // probe window size (if remote window size equals zero)
            if (0 == rmt_wnd)
            {
                if (0 == probe_wait)
                {
                    probe_wait = IKCP_PROBE_INIT;
                    ts_probe = current + probe_wait;
                }
                else
                {
                    if (_itimediff(current, ts_probe) >= 0)
                    {
                        if (probe_wait < IKCP_PROBE_INIT)
                            probe_wait = IKCP_PROBE_INIT;
                        probe_wait += probe_wait / 2;
                        if (probe_wait > IKCP_PROBE_LIMIT)
                            probe_wait = IKCP_PROBE_LIMIT;
                        ts_probe = current + probe_wait;
                        probe |= IKCP_ASK_SEND;
                    }
                }
            }
            else
            {
                ts_probe = 0;
                probe_wait = 0;
            }

            // flush window probing commands
            if ((probe & IKCP_ASK_SEND) != 0)
            {
                seg.cmd = IKCP_CMD_WASK;
                if (offset + IKCP_OVERHEAD > (int)mtu)
                {
                    output(buffer, offset);
                    //Array.Clear(buffer, 0, offset);
                    offset = 0;
                }
                offset += seg.encode(buffer, offset);
            }

            probe = 0;

            // calculate window size
            var cwnd_ = _imin_(snd_wnd, rmt_wnd);
            if (0 == nocwnd)
                cwnd_ = _imin_(cwnd, cwnd_);

            count = 0;
            for (var k = 0; k < snd_queue.Length; k++)
            {
                //已经达到窗口大小的上限，应该停止发送更多数据段
                if (_itimediff(snd_nxt, snd_una + cwnd_) >= 0) break;

                var newseg = snd_queue[k];
                //设置会话 ID
                newseg.conv = conv;
                //设置命令为推送数据（即发送数据）
                newseg.cmd = IKCP_CMD_PUSH;
                //设置接收窗口，通常使用当前的接收窗口大小
                newseg.wnd = seg.wnd;
                //设置时间戳为当前时间
                newseg.ts = current_;
                //设置序列号为 snd_nxt，即当前发送的段的序列号
                newseg.sn = snd_nxt;
                //设置接收端下一个期望的序列号
                newseg.una = rcv_nxt;
                //设置重传时间戳为当前时间，标记该数据段的重传时间
                newseg.resendts = current_;
                //设置重传超时时间
                newseg.rto = rx_rto;
                //初始化快速确认和发送计数
                newseg.fastack = 0;
                newseg.xmit = 0;
                snd_buf = append<Segment>(snd_buf, newseg);
                snd_nxt++;
                count++;
            }

            if (0 < count)
            {
                //表示从发送队列中移除已经发送的部分，只保留剩余未发送的部分。
                snd_queue = slice<Segment>(snd_queue, count, snd_queue.Length);
            }

            // calculate resent
            var resent = (UInt32)fastresend;
            if (fastresend <= 0) resent = 0xffffffff;
            var rtomin = rx_rto >> 3;
            if (nodelay != 0) rtomin = 0;

            // flush data segments
            foreach (var segment in snd_buf)
            {
                var needsend = false;
                var debug = _itimediff(current_, segment.resendts);
                if (0 == segment.xmit)
                {
                    //该数据段的第一次发送
                    needsend = true;
                    segment.xmit++;
                    segment.rto = rx_rto;
                    //设置数据段的重传时间戳为当前时间加上 RTO 和最小 RTO（rtomin），确保在超时后可以重传该数据段
                    segment.resendts = current_ + segment.rto + rtomin;
                }
                else if (_itimediff(current_, segment.resendts) >= 0)
                {
                    //判断当前时间是否已经超过该数据段的重传时间戳（即是否超时）
                    needsend = true;
                    segment.xmit++;
                    xmit++;
                    //如果 nodelay == 0（标准模式），则 RTO 增加 rx_rto，即重新计算重传超时时间
                    //如果 nodelay != 0（启用了 nodelay 模式），则 RTO 只增加一半，即 rx_rto / 2。这种做法使得启用 nodelay 时，重传超时更快，以提高实时性
                    if (0 == nodelay)
                        segment.rto += rx_rto;
                    else
                        segment.rto += rx_rto / 2;
                    //更新重传时间戳为当前时间加上新的 RTO
                    segment.resendts = current_ + segment.rto;
                    //标记此次为丢包重传
                    lost = 1;
                }
                else if (segment.fastack >= resent)
                {
                    // 表示当前数据段的 快速确认（fastack） 次数超过了 fastresend 设置的阈值，意味着该数据段可能已经被接收方多次 ACK，应该进行快速重传

                    //标记需要重发该数据段
                    needsend = true;
                    //增加该数据段的发送次数
                    segment.xmit++;
                    //清除快速确认计数器，因为数据段已经进行快速重传
                    segment.fastack = 0;
                    //更新重传时间戳为当前时间加上 RTO
                    segment.resendts = current_ + segment.rto;
                    //记录进行了快速重传的次数
                    change++;
                }

                if (needsend)
                {
                    segment.ts = current_;
                    segment.wnd = seg.wnd;
                    segment.una = rcv_nxt;

                    var need = IKCP_OVERHEAD + segment.data.Length;
                    if (offset + need > mtu)
                    {
                        output(buffer, offset);
                        //Array.Clear(buffer, 0, offset);
                        offset = 0;
                    }

                    offset += segment.encode(buffer, offset);
                    if (segment.data.Length > 0)
                    {
                        Array.Copy(segment.data, 0, buffer, offset, segment.data.Length);
                        offset += segment.data.Length;
                    }
                    //数据段的发送次数超过了 dead_link 的阈值，说明该数据段可能由于网络问题无法传送，设置 state = 0，表示此时连接可能出现问题或失效
                    if (segment.xmit >= dead_link)
                    {
                        state = 0;
                    }
                }
            }

            // flash remain segments
            //检查是否有剩余数据需要发送
            if (offset > 0)
            {
                output(buffer, offset);
                //Array.Clear(buffer, 0, offset);
                offset = 0;
            }

            // update ssthresh
            //更新慢启动阈值 (ssthresh) 和拥塞窗口 (cwnd)
            if (change != 0)
            {
                //快速重传过

                //计算当前网络中的 "已发送但未确认" 的数据量（即发送序列号和确认序列号之间的差）。
                var inflight = snd_nxt - snd_una;
                //更新 慢启动阈值（ssthresh）。慢启动阈值通常是通过将当前发送的未确认数据量（inflight）除以 2 来进行调整。ssthresh 是 KCP 协议中用于控制 从慢启动到拥塞避免的转换点 的参数。
                ssthresh = inflight / 2;
                //如果更新后的 ssthresh 小于 IKCP_THRESH_MIN（最小阈值），则将其调整为该最小值，避免阈值过小
                if (ssthresh < IKCP_THRESH_MIN)
                    ssthresh = IKCP_THRESH_MIN;
                //将当前的 拥塞窗口（cwnd）设置为 慢启动阈值（ssthresh） 加上 快速重传的阈值（resent），意味着拥塞窗口的大小受网络当前状态影响，并且增加了一些额外的空间来处理快速重传。
                cwnd = ssthresh + resent;
                //更新拥塞窗口的 增量（incr），即下一次能够发送的最大数据量，等于 当前的 cwnd 乘以 最大报文段大小（mss）
                incr = cwnd * mss;
            }

            //处理丢包情况
            if (lost != 0)
            {
                //当发生丢包时，KCP 会将 慢启动阈值（ssthresh） 设置为当前 拥塞窗口（cwnd） 的一半，表示网络变得拥塞，KCP 应该减少发送速率
                ssthresh = cwnd / 2;
                //如果更新后的 ssthresh 小于最小值 IKCP_THRESH_MIN，则将其调整为该最小值
                if (ssthresh < IKCP_THRESH_MIN)
                    ssthresh = IKCP_THRESH_MIN;
                //拥塞窗口被设置为 1，表示进入 拥塞避免阶段，减少数据发送速率，并避免过多的数据发送
                cwnd = 1;
                //设置拥塞窗口的增量为 最大报文段大小（mss），即每次只能发送一个最大单元的数据
                incr = mss;
            }

            //保证最小的拥塞窗口和增量
            if (cwnd < 1)
            {
                cwnd = 1;
                incr = mss;
            }
        }

        // update state (call it repeatedly, every 10ms-100ms), or you can ask
        // ikcp_check when to call it again (without ikcp_input/_send calling).
        // 'current' - current timestamp in millisec.
        public void Update(UInt32 current_)
        {

            current = current_;

            if (0 == updated)
            {
                updated = 1;
                ts_flush = current;
            }

            var slap = _itimediff(current, ts_flush);

            if (slap >= 10000 || slap < -10000)
            {
                ts_flush = current;
                slap = 0;
            }

            if (slap >= 0)
            {
                ts_flush += interval;
                if (_itimediff(current, ts_flush) >= 0)
                    ts_flush = current + interval;
                flush();
            }
        }

        // Determine when should you invoke ikcp_update:
        // returns when you should invoke ikcp_update in millisec, if there
        // is no ikcp_input/_send calling. you can call ikcp_update in that
        // time, instead of call update repeatly.
        // Important to reduce unnacessary ikcp_update invoking. use it to
        // schedule ikcp_update (eg. implementing an epoll-like mechanism,
        // or optimize ikcp_update when handling massive kcp connections)
        public UInt32 Check(UInt32 current_)
        {

            if (0 == updated) return current_;

            var ts_flush_ = ts_flush;
            var tm_flush_ = 0x7fffffff;
            var tm_packet = 0x7fffffff;
            var minimal = 0;

            if (_itimediff(current_, ts_flush_) >= 10000 || _itimediff(current_, ts_flush_) < -10000)
            {
                ts_flush_ = current_;
            }

            if (_itimediff(current_, ts_flush_) >= 0) return current_;

            tm_flush_ = (int)_itimediff(ts_flush_, current_);

            foreach (var seg in snd_buf)
            {
                var diff = _itimediff(seg.resendts, current_);
                if (diff <= 0) return current_;
                if (diff < tm_packet) tm_packet = (int)diff;
            }

            minimal = (int)tm_packet;
            if (tm_packet >= tm_flush_) minimal = (int)tm_flush_;
            if (minimal >= interval) minimal = (int)interval;

            return current_ + (UInt32)minimal;
        }

        // change MTU size, default is 1400
        public int SetMtu(Int32 mtu_)
        {
            if (mtu_ < 50 || mtu_ < (Int32)IKCP_OVERHEAD) return -1;

            var buffer_ = new byte[(mtu_ + IKCP_OVERHEAD) * 3];
            if (null == buffer_) return -2;

            mtu = (UInt32)mtu_;
            mss = mtu - IKCP_OVERHEAD;
            buffer = buffer_;
            return 0;
        }

        public int Interval(Int32 interval_)
        {
            if (interval_ > 5000)
            {
                interval_ = 5000;
            }
            else if (interval_ < 10)
            {
                interval_ = 10;
            }
            interval = (UInt32)interval_;
            return 0;
        }

        // fastest: ikcp_nodelay(kcp, 1, 20, 2, 1)
        // nodelay: 0:disable(default), 1:enable
        // interval: internal update timer interval in millisec, default is 100ms
        // resend: 0:disable fast resend(default), 1:enable fast resend
        // nc: 0:normal congestion control(default), 1:disable congestion control
        public int NoDelay(int nodelay_, int interval_, int resend_, int nc_)
        {

            if (nodelay_ > 0)
            {
                nodelay = (UInt32)nodelay_;
                if (nodelay_ != 0)
                    rx_minrto = IKCP_RTO_NDL;
                else
                    rx_minrto = IKCP_RTO_MIN;
            }

            if (interval_ >= 0)
            {
                if (interval_ > 5000)
                {
                    interval_ = 5000;
                }
                else if (interval_ < 10)
                {
                    interval_ = 10;
                }
                interval = (UInt32)interval_;
            }

            if (resend_ >= 0) fastresend = resend_;

            if (nc_ >= 0) nocwnd = nc_;

            return 0;
        }

        // set maximum window size: sndwnd=32, rcvwnd=32 by default
        public int WndSize(int sndwnd, int rcvwnd)
        {
            if (sndwnd > 0)
                snd_wnd = (UInt32)sndwnd;

            if (rcvwnd > 0)
                rcv_wnd = (UInt32)rcvwnd;
            return 0;
        }

        // get how many packet is waiting to be sent
        public int WaitSnd()
        {
            return snd_buf.Length + snd_queue.Length;
        }
    }
}
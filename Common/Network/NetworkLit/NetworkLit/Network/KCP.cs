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
        /// <summary>
        /// IKCP_CMD_WINS 接收窗口大小告知命令
        /// </summary>
        public const int IKCP_CMD_WINS = 84; // cmd: window size (tell)
        /// <summary>
        /// 表示需要发送探测请求。IKCP_ASK_SEND 是一个用于发送探测请求的命令
        /// </summary>
        public const int IKCP_ASK_SEND = 1;  // need to send IKCP_CMD_WASK
        /// <summary>
        /// 表示需要在后续的 flush() 中发送窗口大小响应 表示请求远程主机告知窗口大小
        /// </summary>
        public const int IKCP_ASK_TELL = 2;  // need to send IKCP_CMD_WINS
        /// <summary>
        /// 默认发送窗口大小
        /// </summary>
        public const int IKCP_WND_SND = 32;
        /// <summary>
        /// 默认接收窗口大小
        /// </summary>
        public const int IKCP_WND_RCV = 32;
        /// <summary>
        /// 默认最大传输单元大小
        /// </summary>
        public const int IKCP_MTU_DEF = 1400;
        public const int IKCP_ACK_FAST = 3;
        /// <summary>
        /// 默认内部定时器的更新间隔
        /// </summary>
        public const int IKCP_INTERVAL = 100;
        /// <summary>
        /// 默认每个数据段的协议头部大小（即附加的头部信息，如序列号、时间戳等）
        /// </summary>
        public const int IKCP_OVERHEAD = 24;
        /// <summary>
        /// 数据段的发送次数超过了 dead_link 的阈值，说明该数据段可能由于网络问题无法传送，设置 state = 0，表示此时连接可能出现问题或失效
        /// </summary>
        public const int IKCP_DEADLINK = 10;
        /// <summary>
        /// 默认慢启动阈值大小
        /// </summary>
        public const int IKCP_THRESH_INIT = 2;
        /// <summary>
        /// 慢启动最小阈值
        /// </summary>
        public const int IKCP_THRESH_MIN = 2;
        /// <summary>
        /// 默认探测等待时间
        /// </summary>
        public const int IKCP_PROBE_INIT = 7000;   // 7 secs to probe window size
        /// <summary>
        /// 默认最大探测等待时间
        /// </summary>
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
        /// <summary>
        /// conv：连接号。UDP是无连接的，conv用于表示来自哪个客户端。对连接的一种替代，因为有conv，因此KCP也是支持多路复用
        /// </summary>
        UInt32 conv;
        /// <summary>
        /// 最大传输单元
        /// </summary>
        UInt32 mtu;
        /// <summary>
        /// 最大报文段大小 mss = mtu - IKCP_OVERHEAD = 1400-24=1376
        /// </summary>
        UInt32 mss;
        /// <summary>
        /// 连接状态 state = 0，表示此时连接可能出现问题或失效
        /// </summary>
        UInt32 state;
        /// <summary>
        /// 是尚未确认的序列号（即已经发送但还没有被接收方确认的最小序列号）
        /// </summary>
        UInt32 snd_una;
        /// <summary>
        /// 是下一个待发送的数据段的序列号
        /// </summary>
        UInt32 snd_nxt;
        /// <summary>
        /// 下一个接受的数据包序列号 即接收窗口的起始点
        /// </summary>
        UInt32 rcv_nxt;
        UInt32 ts_recent; 
        UInt32 ts_lastack;
        /// <summary>
        /// 慢启动阈值
        /// </summary>
        UInt32 ssthresh;
        /// <summary>
        /// RTT 的变化量 (rx_rttval)
        /// </summary>
        UInt32 rx_rttval;
        /// <summary>
        /// 平滑的 RTT 值
        /// </summary>
        UInt32 rx_srtt;
        /// <summary>
        /// 重传超时
        /// </summary>
        UInt32 rx_rto;
        /// <summary>
        /// 最小重传超时
        /// </summary>
        UInt32 rx_minrto;
        /// <summary>
        /// 本地的发送窗口大小
        /// </summary>
        UInt32 snd_wnd;
        /// <summary>
        /// 接收窗口大小
        /// </summary>
        UInt32 rcv_wnd;
        /// <summary>
        /// 远程端的接收窗口大小，表示远程主机当前可以接收的最大数据量
        /// </summary>
        UInt32 rmt_wnd;
        /// <summary>
        /// 本地的拥塞窗口大小，表示可以发送的最大数据量
        /// </summary>
        UInt32 cwnd;
        /// <summary>
        /// 探测标志
        /// </summary>
        UInt32 probe;
        /// <summary>
        /// 当前的时间戳
        /// </summary>
        UInt32 current;
        /// <summary>
        /// 设置内部定时器的更新间隔，单位是毫秒。默认值为 100ms。限定10-5000ms 此值决定了KCP协议在每次更新时的周期。
        /// </summary>
        UInt32 interval;
        /// <summary>
        /// flush刷新时机
        /// </summary>
        UInt32 ts_flush;
        /// <summary>
        /// 总共已经发送的重传次数
        /// </summary>
        UInt32 xmit;
        /// <summary>
        /// 用于启用或禁用KCP的无延迟模式。如果传入的值大于0，表示启用无延迟模式，反之则禁用。
        /// </summary>
        UInt32 nodelay; 
        /// <summary>
        /// 为0表示第一次调用update函数
        /// </summary>
        UInt32 updated;
        /// <summary>
        /// 下一次探测的时间点
        /// </summary>
        UInt32 ts_probe;
        /// <summary>
        /// 探测等待时间
        /// </summary>
        UInt32 probe_wait;
        /// <summary>
        /// 数据段的发送次数超过了 dead_link 的阈值，说明该数据段可能由于网络问题无法传送，设置 state = 0，表示此时连接可能出现问题或失效
        /// </summary>
        UInt32 dead_link;
        /// <summary>
        /// 拥塞窗口的 增量（incr），即下一次能够发送的最大数据量，等于 当前的 cwnd 乘以 最大报文段大小（mss）
        /// </summary>
        UInt32 incr;
        /// <summary>
        /// 发送队列
        /// </summary>
        Segment[] snd_queue = new Segment[0];
        /// <summary>
        /// 接收队列
        /// </summary>
        Segment[] rcv_queue = new Segment[0];
        /// <summary>
        /// 发送缓冲区
        /// </summary>
        Segment[] snd_buf = new Segment[0];
        /// <summary>
        /// 接收缓冲区
        /// </summary>
        Segment[] rcv_buf = new Segment[0];
        /// <summary>
        /// ACK 列表
        /// </summary>
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
        /// <summary>
        /// 看看当前接收队列里是否有一个完整的用户消息
        /// </summary>
        /// <returns></returns>
        public int PeekSize()
        {
            //如果接收队列为空，直接返回 -1，表示无法获取消息的大小
            if (0 == rcv_queue.Length) return -1;

            var seq = rcv_queue[0];
            //判断 seq.frg 是否为 0。frg（fragment）表示数据段的分片索引。如果 frg == 0，则表示当前的这个数据段是最后一个分片或只有一个数据段，它包含了完整的消息
            if (0 == seq.frg) return seq.data.Length;
            //判断接收队列的长度是否小于 seq.frg + 1 seq.frg + 1表示整个用户发送的字节数组的分片数量 即该次消息的总长度 如果当前接收队列大小小于消息的总长度 则表明消息没有接收完毕
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

            //标记是否启用快速恢复
            var fast_recover = false;
            //如果接收队列中的数据量已经达到或超过接收窗口 rcv_wnd 的大小，设置 fast_recover 为 true。这表示接收队列已满，可能需要尽快恢复。
            if (rcv_queue.Length >= rcv_wnd) fast_recover = true;

            // merge fragment.
            //合并接收到的分片数据，直到接收到最后一个分片，并将这些数据拷贝到 buffer 中
            var count = 0;
            var n = 0;
            foreach (var seg in rcv_queue)
            {
                Array.Copy(seg.data, 0, buffer, n, seg.data.Length);
                n += seg.data.Length;
                count++;
                if (0 == seg.frg) break;
            }
            //移除已经处理的分片，更新接收队列
            if (0 < count)
            {
                rcv_queue = slice<Segment>(rcv_queue, count, rcv_queue.Length);
            }

            // move available data from rcv_buf -> rcv_queue
            //将接收缓冲区中可用的分片移动到接收队列中，并更新接收队列和期望接收的序列号
            count = 0;
            foreach (var seg in rcv_buf)
            {
                //如果当前分片的序列号 seg.sn 等于期望的序列号 rcv_nxt，并且接收队列的长度小于接收窗口 rcv_wnd，则表示可以将该分片加入接收队列
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
            //更新接收缓冲区，移除已经处理的分片
            if (0 < count) rcv_buf = slice<Segment>(rcv_buf, count, rcv_buf.Length);

            // fast recover
            //如果接收队列的长度小于接收窗口大小，并且启用了快速恢复（fast_recover 为 true），则准备进行窗口大小的更新
            if (rcv_queue.Length < rcv_wnd && fast_recover)
            {
                // ready to send back IKCP_CMD_WINS in ikcp_flush
                // tell remote my window size
                //表示需要通知远程主机发送窗口大小
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

            //如果分片数量超过 255，返回 -2，表示错误
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
                //剩余还有多少分片
                seg.frg = (UInt32)(count - i - 1);
                snd_queue = append<Segment>(snd_queue, seg);
            }

            return 0;
        }

        // update ack.
        /// <summary>
        /// 更新 KCP 协议中的 RTT（往返时延） 和 RTO（重传超时）
        /// </summary>
        /// <param name="rtt"></param>
        void update_ack(Int32 rtt)
        {
            //检查是否为第一次更新 RTT。如果 rx_srtt（平滑的 RTT 值）为零，说明这是第一次接收到 RTT 数据
            if (0 == rx_srtt)
            {
                //如果是第一次更新，直接将当前 RTT (rtt) 赋值给 rx_srtt（平滑 RTT）
                rx_srtt = (UInt32)rtt;
                //初始时，RTT 的变化量 (rx_rttval) 设置为 rtt 的一半，表示初始的 RTT 估计波动量
                rx_rttval = (UInt32)rtt / 2;
            }
            else
            {
                //计算当前 RTT 与平滑 RTT (rx_srtt) 之间的差值 delta，即：delta = rtt - rx_srtt。这个差值反映了当前 RTT 和估计 RTT 之间的偏差
                Int32 delta = (Int32)((UInt32)rtt - rx_srtt);
                //如果 delta 是负数（即当前 RTT 小于平滑 RTT），则将其转换为正值。这样做是为了计算波动量时对绝对值进行操作
                if (0 > delta) delta = -delta;
                //更新 RTT 波动量 (rx_rttval)。这一行代码使用了加权平均算法：新的 rx_rttval 由之前的 75 % 和当前 RTT 差值的 25 % 加权计算而得（3 / 4 和 1 / 4）。这可以平滑变化量，避免剧烈的波动影响。
                rx_rttval = (3 * rx_rttval + (uint)delta) / 4;
                //更新平滑 RTT (rx_srtt)。这一行代码也是采用了加权平均：新的 rx_srtt 是之前平滑 RTT 的 87.5 %（7 / 8）和当前 RTT 的 12.5 %（1 / 8）加权平均结果。通过这种方式，KCP 更倾向于接受最近的 RTT 测量值，但不会太依赖单一的 RTT 值，从而避免突发的网络波动导致 RTO 的剧烈波动。
                rx_srtt = (UInt32)((7 * rx_srtt + rtt) / 8);
                //确保 rx_srtt（平滑 RTT）不会小于 1 毫秒，避免出现不合理的极小值。
                if (rx_srtt < 1) rx_srtt = 1;
            }
            //计算 RTO（重传超时）
            //4 * rx_rttval：是对 RTT 波动量的放大，体现了网络的不稳定性和变化。_imax_(1, 4 * rx_rttval)：确保 4 * rx_rttval 至少为 1 毫秒，避免过小的 RTO 值。结果是 RTO = 平滑 RTT + 变化量放大（4 * rx_rttval），即当前网络的传输延迟加上对网络波动的适应。
            var rto = (int)(rx_srtt + _imax_(1, 4 * rx_rttval));
            //确保计算得到的 RTO 在一个合理的范围内：rx_minrto：RTO 的最小值，确保 RTO 不会小于这个值，避免网络较慢时超时过短。IKCP_RTO_MAX：RTO 的最大值，避免 RTO 超过该值，防止网络异常时导致过长的超时。
            rx_rto = _ibound_(rx_minrto, (UInt32)rto, IKCP_RTO_MAX);
        }

        /// <summary>
        /// 用于更新snd_una（未确认序列号）。它的作用是根据发送缓冲区 snd_buf 的内容来调整 snd_una，确保 snd_una 始终指向 未确认的最小序列号
        /// </summary>
        void shrink_buf()
        {
            //如果 snd_buf 非空，则将 snd_una 更新为 snd_buf[0].sn。这里的 snd_buf[0].sn 是发送缓冲区中第一个数据段的序列号，代表最早未被确认的数据段的序列号。这是因为，KCP 协议中发送缓冲区的序列号是升序排列的，因此 snd_buf[0].sn 即为最小的未确认序列号。通过更新 snd_una，协议可以知道当前哪个数据段还没有被确认。
            //如果 snd_buf 为空，则将 snd_una 设置为 snd_nxt。snd_nxt 是下一个待发送的序列号，表示尚未发送的下一个数据段的序列号。由于发送缓冲区为空，说明当前没有任何待确认的数据段，因此 snd_una 应该指向下一个待发送的数据段，即 snd_nxt。
            if (snd_buf.Length > 0)
                snd_una = snd_buf[0].sn;
            else
                snd_una = snd_nxt;
        }

        /// <summary>
        /// 如果接收到的确认号与某个数据段匹配，则将该数据段从发送缓冲区 snd_buf 中移除，表示该数据段已经被确认。如果确认号与某个数据段不匹配，则增加该数据段的 快速确认计数（fastack），以便可能触发快速重传机制。
        /// </summary>
        /// <param name="sn"></param>
        void parse_ack(UInt32 sn)
        {
            //_itimediff(sn, snd_una) < 0：首先检查传入的 sn（确认号）是否小于 snd_una，即未确认数据的序列号。如果 sn 小于 snd_una，表示该确认号已经过时，无法处理，因此返回。
            //_itimediff(sn, snd_nxt) >= 0：然后检查 sn 是否大于等于 snd_nxt，即是否是一个尚未发送或已经发送的数据段。如果 sn 大于等于 snd_nxt，表示该确认号还没有到达有效的数据段，因此也不需要处理，直接返回。
            if (_itimediff(sn, snd_una) < 0 || _itimediff(sn, snd_nxt) >= 0) return;

            var index = 0;
            foreach (var seg in snd_buf)
            {
                if (sn == seg.sn)
                {
                    //确认收到就移除该数据段
                    snd_buf = append<Segment>(slice<Segment>(snd_buf, 0, index), slice<Segment>(snd_buf, index + 1, snd_buf.Length));
                    break;
                }
                else
                {
                    //如果当前数据段的序列号 seg.sn 不等于接收到的确认号 sn，则表示该数据段没有被确认。在这种情况下，增加 seg.fastack（快速确认计数）
                    seg.fastack++;
                }

                index++;
            }
        }

        /// <summary>
        /// 用于处理接收到的 una（未确认序列号），根据 una 来 更新发送缓冲区（snd_buf），移除那些已经确认的数据段。它的目标是删除所有序列号小于或等于 una 的数据段，因为这些数据段已经被远端接收并确认，无需再保留在发送缓冲区。
        /// </summary>
        /// <param name="una"></param>
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

        /// <summary>
        /// 加入待确认列表
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="ts"></param>
        void ack_push(UInt32 sn, UInt32 ts)
        {
            acklist = append<UInt32>(acklist, new UInt32[2] { sn, ts });
        }

        void ack_get(int p, ref UInt32 sn, ref UInt32 ts)
        {
            sn = acklist[p * 2 + 0];
            ts = acklist[p * 2 + 1];
        }

        /// <summary>
        /// 用于 处理接收到的数据段，并将数据段按序列号存储到接收缓冲区 rcv_buf 中。它还会根据接收到的数据段情况 更新接收窗口 rcv_queue，确保数据可以顺利传输并进行有效的流控制
        /// </summary>
        /// <param name="newseg"></param>
        void parse_data(Segment newseg)
        {
            //newseg.sn 是接收到的当前数据段的 序列号。这个序列号用于判断该数据段是否已经是 新数据，以及是否应该被存入接收缓冲区
            var sn = newseg.sn;
            if (_itimediff(sn, rcv_nxt + rcv_wnd) >= 0 || _itimediff(sn, rcv_nxt) < 0) return;
            //获取接收缓冲区的最后一个数据段的索引
            var n = rcv_buf.Length - 1;
            //用来记录接收缓冲区中应该插入数据段的位置
            var after_idx = -1;
            //用来标记数据段是否已经接收过（即是否重复）
            var repeat = false;
            for (var i = n; i >= 0; i--)
            {
                var seg = rcv_buf[i];
                if (seg.sn == sn)
                {
                    //如果找到已经存在的相同序列号的段，表示该数据段是 重复的，则将 repeat 设置为 true 并退出循环
                    repeat = true;
                    break;
                }

                if (_itimediff(sn, seg.sn) > 0)
                {
                    //如果接收到的数据段的序列号 sn 大于 seg.sn，说明应该插入到当前数据段的后面，记录当前位置为 after_idx 并退出循环
                    after_idx = i;
                    break;
                }
            }

            if (!repeat)
            {
                //如果接收到的数据段没有重复，即没有在接收缓冲区中找到相同的序列号，那么就将该数据段插入到接收缓冲区中

                //如果 after_idx == -1，说明数据段应该被插入到缓冲区的最前面，直接在缓冲区前面添加该数据段
                //否则，使用 slice 函数将缓冲区分成两部分：一部分是从缓冲区开始到 after_idx + 1 的部分，另一部分是从 after_idx + 1 到缓冲区末尾的部分。然后将新数据段插入到这两部分之间
                if (after_idx == -1)
                    rcv_buf = append<Segment>(new Segment[1] { newseg }, rcv_buf);
                else
                    rcv_buf = append<Segment>(slice<Segment>(rcv_buf, 0, after_idx + 1), append<Segment>(new Segment[1] { newseg }, slice<Segment>(rcv_buf, after_idx + 1, rcv_buf.Length)));
            }

            // move available data from rcv_buf -> rcv_queue
            //该部分的目的是将 已经按顺序接收的 数据段从接收缓冲区 rcv_buf 中移到 接收队列 rcv_queue 中，以便应用层可以读取。
            var count = 0;
            foreach (var seg in rcv_buf)
            {
                //如果数据段的序列号等于 期望接收的序列号 rcv_nxt，则说明该数据段是 连续的，可以将其加入接收队列 rcv_queue
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
            //移除已处理的数据段
            if (0 < count)
            {
                rcv_buf = slice<Segment>(rcv_buf, count, rcv_buf.Length);
            }
        }

        // when you received a low level packet (eg. UDP packet), call it
        public int Input(byte[] data)
        {
            // 记录当前发送的未确认序列号
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
                // 更新远程窗口大小
                rmt_wnd = (UInt32)wnd;
                parse_una(una);
                shrink_buf();

                if (IKCP_CMD_ACK == cmd)
                {
                    //接收到对某个数据包的确认
                    if (_itimediff(current, ts) >= 0)
                    {
                        update_ack(_itimediff(current, ts));
                    }
                    parse_ack(sn);
                    shrink_buf();
                }
                else if (IKCP_CMD_PUSH == cmd)
                {
                    //处理 PUSH 数据包

                    //如果接收到的序列号小于接收窗口的结束点，表示数据包顺序正确。
                    if (_itimediff(sn, rcv_nxt + rcv_wnd) < 0)
                    {
                        ack_push(sn, ts);
                        //如果接收到的数据包序列号大于或等于接收窗口的起始点，处理该数据包。
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
                    //接收到窗口请求包时，设置 probe 为 IKCP_ASK_TELL，表示需要在后续的 flush() 中发送窗口大小响应
                    probe |= IKCP_ASK_TELL;
                }
                else if (IKCP_CMD_WINS == cmd)
                {
                    //接收到窗口大小响应包时，不做任何操作
                    // do nothing
                }
                else
                {
                    return -3;
                }

                offset += (int)length;
            }

            //最后检查并更新拥塞窗口
            //如果 snd_una（发送端未确认的序列号） 和 s_una（之前发送端未确认的序列号） 之间的差值大于零，表示发送端已经接收到一些确认
            if (_itimediff(snd_una, s_una) > 0)
            {
                //判断是否可以增加 cwnd（拥塞窗口）
                if (cwnd < rmt_wnd)
                {
                    //如果本地的拥塞窗口小于远程接收窗口，说明可以尝试增加本地的 cwnd 来允许更多的数据发送
                    var mss_ = mss;
                    if (cwnd < ssthresh)
                    {
                        //如果当前的 cwnd 小于阈值 ssthresh，表示协议处于 慢启动阶段。在这个阶段，cwnd 会指数级增长，每次确认一个数据包后，cwnd 增加 1，直到达到慢启动阈值为止
                        //在 TCP 或 KCP 这样的协议中，每个成功发送的数据包都会收到 一个 ACK。在 慢启动阶段：发送 cwnd 个数据包这 cwnd 个数据包的 ACK 返回后，cwnd 每收到 1 个 ACK，就增长 1意味着 cwnd 每个 RTT（往返时间）翻倍
                        cwnd++;
                        incr += mss_;
                    }
                    else
                    {
                        //当 cwnd 达到或超过 ssthresh 时，协议进入 拥塞避免阶段

                        //确保 incr 至少为 mss_，以防止增发的速率过小
                        if (incr < mss_)
                        {
                            incr = mss_;
                        }
                        //计算下一个增长量 incr，这是根据 当前的拥塞窗口大小 来调整的。此计算方式使得增长速度逐渐减缓，避免过快增加导致网络拥塞。
                        //(mss_ * mss_) / incr：让 incr 的增长速度随着 incr 逐渐增大而变得缓慢。
                        //(mss_ / 16)：用于微调增发的速率，防止突增。
                        incr += (mss_ * mss_) / incr + (mss_ / 16);
                        //如果当前的增发量 incr 达到或超过下一个拥塞窗口大小（(cwnd + 1) * mss_），就将拥塞窗口 cwnd 增加 1
                        if ((cwnd + 1) * mss_ <= incr) cwnd++;
                    }
                    if (cwnd > rmt_wnd)
                    {
                        //本地拥塞窗口 cwnd 大于 远程接收窗口 rmt_wnd，就将本地的拥塞窗口设置为远程接收窗口的大小
                        cwnd = rmt_wnd;
                        //同时，更新 incr，使其符合新的拥塞窗口大小（rmt_wnd* mss_），确保增发速率不会超过远程主机的接收能力
                        incr = rmt_wnd * mss_;
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// 用于计算接收窗口（rcv_wnd）中尚未使用的空间大小。接收窗口代表了接收端可以接收的最大数据量，rcv_queue 存储了已经接收到的数据分段。此函数用于确定接收端窗口中剩余的可用空间，帮助控制发送的数据量，避免接收端接收过多而导致溢出
        /// </summary>
        /// <returns></returns>
        Int32 wnd_unused()
        {
            //检查接收队列（rcv_queue）中的数据量是否小于接收窗口（rcv_wnd）rcv_wnd 是接收窗口的大小，rcv_queue.Length 是当前接收队列中的数据量。计算剩余的接收窗口空间的方法是用接收窗口的总大小减去当前接收队列中已经存储的数据量。这返回了接收端可以接收的新数据的最大大小。如果接收队列还有空闲空间，返回的值将是接收窗口剩余的空间，单位是数据分段的数量
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

            //准备一个数据段 seg，用于存储即将发送的确认消息（ACK）
            var seg = new Segment(0);
            seg.conv = conv;
            seg.cmd = IKCP_CMD_ACK;
            //seg.wnd=rcv_wnd - rcv_queue.Length
            seg.wnd = (UInt32)wnd_unused();
            seg.una = rcv_nxt;

            // flush acknowledges
            //遍历 ACK 列表，逐个将确认消息（包含序列号和时间戳）编码到缓冲区中，并根据需要发送出去。
            var count = acklist.Length / 2;
            //offset 是当前缓冲区中已填充的数据长度 如果当前缓冲区的大小加上协议头（offset + IKCP_OVERHEAD）超过了 mtu，则需要将当前缓冲区的数据发送出去
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
            //一旦所有的确认消息都发送完成，就清空 acklist，为下一轮的 ACK 数据做准备
            acklist = new UInt32[0];

            // probe window size (if remote window size equals zero)
            //检查远程窗口大小是否为零
            if (0 == rmt_wnd)
            {
                //KCP 协议中的窗口机制用于控制接收方的接收能力。如果接收方的窗口 rmt_wnd 为零，表示接收方的缓冲区已满，无法接收更多数据。在这种情况下，发送方通过探测机制来询问接收方何时可以继续接收数据 探测的基本流程：当 rmt_wnd 为零时，发送方会定期发送探测请求。probe_wait 控制探测请求的发送间隔，从 IKCP_PROBE_INIT 开始逐渐增加，但不超过 IKCP_PROBE_LIMIT。如果接收方能够接收数据，则会响应探测请求，远程接收窗口大小 rmt_wnd 会更新为非零值，探测过程结束。
                if (0 == probe_wait)
                {
                    //probe_wait 是探测等待时间。若 probe_wait 为零，表示这是第一次进行探测
                    //在这种情况下，probe_wait 被初始化为 IKCP_PROBE_INIT（一个初始的探测时间）。ts_probe 记录了下一次探测的时间点，初始时为 current +probe_wait，即在当前时间之后的 IKCP_PROBE_INIT 时间
                    probe_wait = IKCP_PROBE_INIT;
                    ts_probe = current + probe_wait;
                }
                else
                {
                    //如果 probe_wait 已经初始化并且不为零，则检查是否到了可以再次探测的时机
                    //判断当前时间是否已经超过了 ts_probe，即是否已经到了下次探测的时刻
                    if (_itimediff(current, ts_probe) >= 0)
                    {
                        //如果 probe_wait 小于初始探测值（IKCP_PROBE_INIT），则重置为初始值
                        if (probe_wait < IKCP_PROBE_INIT)
                            probe_wait = IKCP_PROBE_INIT;
                        //probe_wait 会增加一半的值：probe_wait += probe_wait / 2。这意味着每次探测的时间间隔会越来越长，避免频繁探测
                        probe_wait += probe_wait / 2;
                        //如果 probe_wait 超过了最大探测值 IKCP_PROBE_LIMIT，则将其限制为 IKCP_PROBE_LIMIT，防止探测时间间隔过长
                        if (probe_wait > IKCP_PROBE_LIMIT)
                            probe_wait = IKCP_PROBE_LIMIT;
                        //更新探测时间：ts_probe 会更新为当前时间加上新的 probe_wait，表示下一次探测的时间
                        ts_probe = current + probe_wait;
                        //设置探测标志：probe |= IKCP_ASK_SEND; 设置探测标志，表示需要发送探测请求。IKCP_ASK_SEND 是一个用于发送探测请求的命令
                        probe |= IKCP_ASK_SEND;
                    }
                }
            }
            else
            {
                //如果 rmt_wnd 不为零，表示远程接收方的接收窗口已经打开，数据可以正常发送
                //在这种情况下，探测逻辑停止，ts_probe 和 probe_wait 都被重置为零，表示不再进行窗口探测
                ts_probe = 0;
                probe_wait = 0;
            }

            // flush window probing commands
            //负责处理 窗口探测命令（window probing commands） 的发送。当接收窗口为零时，发送方通过探测机制询问接收方的接收窗口大小。此时，探测请求需要通过发送特定的命令来进行。
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
            //发送完探测请求后，重置 probe 标志，表示此时的探测任务已经完成。此操作确保探测请求只会发送一次，直到新的探测条件满足时（比如远程接收窗口再次为零）
            probe = 0;

            // calculate window size
            //用于计算和更新发送窗口大小，并根据当前窗口大小从发送队列中选择数据段进行发送
            var cwnd_ = _imin_(snd_wnd, rmt_wnd);
            //如果 nocwnd 为 0，表示启用窗口控制
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
            //计算快速重传的阈值 和 设置最小 RTO（重传超时时间），用于控制 KCP 可靠传输机制中的数据重传策略
            var resent = (UInt32)fastresend;
            //fastresend <= 0 时，意味着不启用快速重传，所以将 resent 设为 0xffffffff（最大值），确保不会触发快速重传机制
            if (fastresend <= 0) resent = 0xffffffff;
            //如果启用延时重传 那就延时rx_rto的1/8
            var rtomin = rx_rto >> 3;
            //如果不启用延时重传 那就延时0
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
                    //设置数据段的重传时间戳为当前时间加上 RTO 和延时时间，确保在超时后可以重传该数据段
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
                //这个判断条件检查 updated 是否为零，表示是否第一次调用 Update 函数
                updated = 1;
                //设置 ts_flush 为当前时间戳，表示首次调用时 ts_flush 初始化为当前时间
                ts_flush = current;
            }
            //该变量存储当前时间与上次刷新时间的差值，表示自上次刷新以来经过的时间
            var slap = _itimediff(current, ts_flush);

            //判断 slap 是否大于等于 10000 毫秒（10秒），或者小于 -10000 毫秒（即时间出现了不一致，可能是由于系统时间变化或其他原因）如果系统时间出现异常（跳跃过大），重新初始化 ts_flush 时间并将 slap 设置为零
            if (slap >= 10000 || slap < -10000)
            {
                ts_flush = current;
                slap = 0;
            }

            if (slap >= 0)
            {
                //如果 slap 大于等于 0，表示当前时间已经超过了上次刷新时间 ts_flush，可以进行刷新操作
                //更新 ts_flush，将其推迟 interval 毫秒。interval 是固定的刷新间隔时间，决定了多久会进行一次刷新
                ts_flush += interval;
                //判断当前时间是否已经超过了新的 ts_flush 时间。如果超过，表示刷新时机已经到来，应该立即进行刷新 如果刷新时机到了，将 ts_flush 更新为当前时间加上 interval，为下一次刷新设置新的时间点
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
        /// <summary>
        /// 用于配置KCP协议的延迟、重传、拥塞控制等参数，调整协议的行为，以便在特定的应用场景中实现更高效的传输。该方法通过调整以下参数来优化连接的延迟和传输效率
        /// </summary>
        /// <param name="nodelay_"></param>
        /// <param name="interval_"></param>
        /// <param name="resend_"></param>
        /// <param name="nc_"></param>
        /// <returns></returns>
        public int NoDelay(int nodelay_, int interval_, int resend_, int nc_)
        {

            if (nodelay_ > 0)
            {
                nodelay = (UInt32)nodelay_;
                //如果启用无延迟模式（nodelay_ != 0），则会设置最小重传超时（rx_minrto）为 IKCP_RTO_NDL，这是一个较小的值，用于减少延迟
                //如果禁用无延迟模式，则将最小重传超时（rx_minrto）恢复为默认值 IKCP_RTO_MIN
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
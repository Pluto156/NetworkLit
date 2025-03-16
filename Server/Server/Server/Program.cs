using System;
using System.Net;
using System.Threading;
using NetworkLit.Network;

class Program
{
    static void Main(string[] args)
    {
        // 启动服务器
        Server server = new Server();
        server.RegisterHandler(MessageType.Connect, NewConnection);
        server.Listen(8888);

        // 每一帧更新服务器
        float dt = 0.0f;
        DateTime lastTime = DateTime.Now;

        // 主线程中处理服务器的更新

        while (true)
        {
            // 计算 dt（时间差）
            DateTime currentTime = DateTime.Now;
            dt = (float)(currentTime - lastTime).TotalSeconds;
            lastTime = currentTime;

            // 更新服务器
            server.Update(dt);

            // 控制更新频率，避免 CPU 占用过高
            Thread.Sleep(16); // 16ms 大约是 60 FPS
        }




        // 新连接时的回调
        static void NewConnection(MessageType id, ByteBuffer bf, IPEndPoint endPoint)
        {
            Console.WriteLine("连接成功");
        }
    }
}
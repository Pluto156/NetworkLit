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
        
        server.Listen(8888);

        // 每一帧更新服务器
        float dt = 0.0f;
        DateTime lastTime = DateTime.Now;

        // 主线程中处理服务器的更新
        Thread serverThread = new Thread(() =>
        {
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
        });
        serverThread.Start();

        // 启动客户端线程
        Thread clientThread = new Thread(() =>
        {
            Client client = new Client();
            client.RegisterHandler(MessageType.Connect, NewConnection);
            client.Connect("127.0.0.1", 8888); // 连接到本地的服务器

            // 每一帧更新客户端
            float clientDt = 0.0f;
            DateTime clientLastTime = DateTime.Now;

            while (true)
            {
                // 计算 dt（时间差）
                DateTime clientCurrentTime = DateTime.Now;
                clientDt = (float)(clientCurrentTime - clientLastTime).TotalSeconds;
                clientLastTime = clientCurrentTime;

                // 更新客户端
                client.Update(clientDt);

                // 控制更新频率，避免 CPU 占用过高
                Thread.Sleep(16); // 16ms 大约是 60 FPS
            }
        });
        clientThread.Start();

        // 防止主程序退出
        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();
    }

    // 新连接时的回调
    static void NewConnection(MessageType id, ByteBuffer bf, IPEndPoint endPoint)
    {
        Console.WriteLine("连接成功");
    }
}

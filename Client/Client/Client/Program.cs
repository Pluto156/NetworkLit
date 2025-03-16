using System;
using System.Net;
using System.Threading;
using NetworkLit.Network;

class Program
{
    static void Main(string[] args)
    {

        Client client = new Client();
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
        

    }


}

using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLit.Utility
{
    public static class PackageCompress
    {
        public static byte[] Compress(byte[] data)
        {
            /*byte[] ret;

            using (var ms = new MemoryStream())
            {
                using (var ds = new DeflateStream(ms, CompressionMode.Compress))
                {
                    ds.Write(data, 0, data.Length);
                }

                ret = ms.ToArray();
            }

            return ret;*/
            return data;
        }

        public static byte[] Decompress(byte[] data)
        {
            /*if (data == null || data.Length == 0)
            {
                return data;
            }

            byte[] ret;

            using (var ms = new MemoryStream(data))
            {
                using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
                {
                    var ws = new MemoryStream();
                    var buffer = new byte[4096];
                    int count = 0;

                    while ((count = ds.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ws.Write(buffer, 0, count);
                    }

                    ret = ws.ToArray();
                }
            }

            return ret;*/
            return data;
        }
    }
}

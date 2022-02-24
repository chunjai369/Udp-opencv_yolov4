using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Drawing;
using System.Text.Json;

namespace WindowsFormsApp1
{

    public class Upd
    {
        private IPAddress serverIP = IPAddress.Parse("192.168.0.100");
        private static int max_size = 65000;
        private UdpClient udpClient = new UdpClient(11000);

        private class Packs_Info {
            public int packs_num { get; set; }
        }

        public  byte[] ImageToByte(Image img) {
            using (var stream = new MemoryStream()) {
                img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }

        public void SendData(Image img) {
            try {
                udpClient.Connect(serverIP, 11000);
                var buffer = ImageToByte(img);
                var  num_packs = 1;
                if (buffer.Length > max_size) {
                    num_packs = (int)Math.Ceiling((double)buffer.Length / max_size);
                }
                var packs_info = new Packs_Info { packs_num =  num_packs };
                var packs_info_json = JsonSerializer.Serialize<Packs_Info>(packs_info);
                var packs_info_Bytes = Encoding.ASCII.GetBytes(packs_info_json);
                udpClient.Send(packs_info_Bytes, packs_info_Bytes.Length);

                var left = 0;
                var right = max_size;

                if (num_packs == 1) {
                    udpClient.Send(buffer, buffer.Length);
                    Console.WriteLine("package number: {0} ,  Size:  {1}   ",num_packs,buffer.Length);
                } else {
                    for (int i = 0; i < num_packs; i++)
                    {
                        Console.WriteLine("left : {0} , right : {1}", left, right);
                        var temp = 0;
                        var data = new byte[max_size];
                        for (int j = left; j < right; j++)
                        {
                            if (j == buffer.Length)
                                break;
                            data[temp] = buffer[j];
                            temp++;
                        }
                        left = right;
                        right += max_size;
                        udpClient.Send(data, data.Length);
                        Console.WriteLine("package number: {0} ,  Size:  {1}   ", i, data.Length);
                    }
                }
            }
            catch (Exception e) {
                Console.WriteLine(e.ToString());
            }
        }

        public string ReceiveData() {
            var RemoteIpEndPoint = new IPEndPoint(serverIP, 11000);
            var receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);
            var  jsonString= Encoding.ASCII.GetString(receiveBytes);
            return jsonString;
            //var box_info = JsonSerializer.Deserialize<Box_Info>(jsonString);
            //Console.WriteLine("box {0} ,  color:  {1}   ", box_info.box, box_info.color);
        }

        public string stopStream() {
            udpClient.Close();
            return "stoped";
        }

    }
}

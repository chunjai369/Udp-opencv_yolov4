using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Windows.Forms;
using Emgu.CV;
using System.Text.Json;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private VideoCapture objCapture;
        private Mat _frame;
        private Mat _frame_before;
        private bool isOpen = true;
        private bool is_stream = true;
        private int Fps;
        private Thread thread_stream;
        private Thread thread_receive;
        private string box_info_str;
        public static object lockObj = new Object();
        private Upd udp;
        private class Box_Info 
        {
            public string name { get; set; }
            public int[] box { get; set; }
            public string label { get; set; }
            public int[] color { get; set; }
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void ProcessFrame(object sender, EventArgs e) {
            if (objCapture != null && objCapture.Ptr != IntPtr.Zero) {
                objCapture.Retrieve(_frame_before, 0);
                Fps = (int)objCapture.Get(CapProp.Fps);
                CvInvoke.Flip(_frame_before, _frame, FlipType.Horizontal);
                if (box_info_str !=null) {
                    var box_info = JsonSerializer.Deserialize<Box_Info>(box_info_str);
                    var rectangle = new Rectangle(box_info.box[0], box_info.box[1], box_info.box[2], box_info.box[3]);
                    var mcvScalar = new MCvScalar(box_info.color[0], box_info.color[1], box_info.color[2]);
                    CvInvoke.Rectangle(_frame, rectangle, mcvScalar, 2);
                    CvInvoke.PutText(_frame, box_info.label, new Point(box_info.box[0], box_info.box[1] - 10), FontFace.HersheyComplexSmall, 1, mcvScalar, 2);
                }
                CvInvoke.PutText(_frame, Fps.ToString(), new Point(0,25), FontFace.HersheyComplexSmall, 1.5, new MCvScalar(0,0,255), 2);
                pictureBox1.Image = _frame.ToBitmap();

            }
        }

        private void button1_Click(object sender, EventArgs e) {
            if (isOpen){
                button1.Text = "Trun Off Camera";
                objCapture = new VideoCapture();
                objCapture.ImageGrabbed += ProcessFrame;
                _frame = new Mat();
                _frame_before = new Mat();
                if (objCapture != null)
                    objCapture.Start();
            }
            else {
                button1.Text = "Trun On Camera";
                objCapture.Stop();
            }
            isOpen = !isOpen;
        }

        private void button2_Click(object sender, EventArgs e) {
            if (is_stream){
                udp = new Upd();
                button2.Text = "Stream Stop";
                 thread_stream = new Thread(stream);
                thread_receive = new Thread(receive);
                thread_stream.Start();
                thread_receive.Start();
            } else {
                thread_stream.Abort();
                thread_receive.Abort();
                udp.stopStream();
                button2.Text = "Stream Start";
            }
            is_stream = !is_stream;
        }

        private void stream() {
            try {
                while (true) {
                    udp.SendData(_frame_before.ToBitmap());
                    Thread.Sleep(1000/(Fps));
                }
            } catch(ThreadAbortException e) {
                Console.WriteLine("Stream Stopping");
            } finally {
                Console.WriteLine("Stream Stopped");
            }
        }

        private void receive() {
            try {
                while (true) {
                    lock (lockObj) {
                        box_info_str = udp.ReceiveData();
                    }
                    Console.WriteLine(box_info_str);
                }
            }
            catch (ThreadAbortException e) {
                Console.WriteLine("Receive Stopping");
            }
            finally {
                Console.WriteLine("Receive Stopped");
            }
        }

    }
}

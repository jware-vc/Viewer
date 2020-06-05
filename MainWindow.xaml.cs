using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Raytracer;
using System.Threading;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.CompilerServices;

namespace GradientView
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static int DPI = 96;
        public int idx = 0;
        public DispatcherTimer updateTimer = new DispatcherTimer();
        public LayerModel layerModel;
        public Program prog;
        public Task renderTask;
        private bool isDragging = false;
        private string layerName = "lightPass";
        private System.Windows.Point dragPos;
        private static DispatcherTimer timer;
        private static WorkQueue workQueue;


        public MainWindow()
        {
            InitializeComponent();
            layerModel = new LayerModel();
            prog = new Program();
            int width = 512;
            int height = 512;
            layerModel.addLayer(layerName, prog, prog.uv, width, height, 1);
            View.Source = layerModel.bitmaps[layerName];
            workQueue = new WorkQueue();
            workQueue.addWork(prog.lightPass, layerModel.taskChunks[layerName]);

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(30);
            timer.Tick += (sender, arguments) => layerModel.updateBpm(layerName);
            timer.Start();
        }


        private void handleSizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            dragPos = e.GetPosition(this);
            isDragging = true;
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            workQueue.cancel();
            workQueue.addWork(prog.lightPass, layerModel.taskChunks[layerName]);
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {

                var pos = e.GetPosition(this);
                var offset = new Vector3((float)(dragPos.X - pos.X), (float)(dragPos.Y - pos.Y), 0);
                layerModel.cam.origin += offset / 10.0f;
                dragPos = pos;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            workQueue.cancel();
            workQueue.close();
        }
    }
}

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
        private WriteableBitmap bitmap;


        public MainWindow()
        {
            InitializeComponent();
            layerModel = new LayerModel();
            prog = new Program();
            int width = 1024;
            int height = 1024;
            bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            var frame = layerModel.AddFrame(layerName, prog, prog.color, width, height, 500, bitmap.Format.BitsPerPixel);
            View.Source = bitmap;
            workQueue = new WorkQueue();
            workQueue.AddWork(frame.GetRenders());

            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(60)
            };
            timer.Tick += (sender, arguments) => layerModel.updateBpm(bitmap);
            timer.Start();
        }


        private void handleSizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            workQueue.Cancel();
            layerModel.layer.Reset();
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            workQueue.Cancel();
            workQueue.Close();
        }
    }
}

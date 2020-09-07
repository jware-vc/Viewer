using Raytracer;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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
        private static DispatcherTimer timer;
        private static WorkQueue workQueue;
        private readonly WriteableBitmap bitmap;
        private readonly List<Layer> layers = new List<Layer>();


        public MainWindow()
        {
            InitializeComponent();
            layerModel = new LayerModel();
            //prog = Program.Cornel();
            int width = 2048;
            int height = 2048;
            prog = Program.parseScene(@"scenes/book.txt", new Vector2(width, height));
            bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgba128Float, null);
            var layer = layerModel.AddFrame(prog, prog.Color, width, height, 100, bitmap.Format.BitsPerPixel);
            layers.Add(layer);
            View.Source = bitmap;
            workQueue = new WorkQueue();
            foreach (var l in layers)
            {
                workQueue.AddWork(l.renders);
            }

            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            timer.Tick += (sender, arguments) => layerModel.UpdateBpm(bitmap, layer);
            timer.Start();
        }


        private void HandleSizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            workQueue.Cancel();
            foreach (var l in layers)
            {
                l.Reset();
            }
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

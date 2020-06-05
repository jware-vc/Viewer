using Raytracer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GradientView
{
    class Worker
    {
        public bool cancelWork = false;
        public bool abortWork = false;
        public bool wait = false;
        private ConcurrentBag<WorkInfo> workQueue;
        private WorkInfo wInfo;

        public Worker (ConcurrentBag<WorkInfo> _workQueue)
        {
            workQueue = _workQueue;
        }
        public void run()
        {
            wInfo = new WorkInfo();

            while (abortWork != true)
            {
                if (wait is false && workQueue.TryTake(out wInfo))
                {
                    LayerModel.Render2(wInfo.renderDelegate, wInfo.info);
                }
                Thread.Sleep(20);
            }
        }

    }
    struct WorkInfo
    {
        public LayerModel.RenderDelegate renderDelegate;
        public RenderInfo info;
        public WorkInfo(LayerModel.RenderDelegate _renderDelegate, RenderInfo _info) 
        {
            renderDelegate = _renderDelegate;
            info = _info;
        }
    }
    public class WorkQueue
    {
        private static int numThreads = 15;
        private Thread[] threads = new Thread[numThreads];
        private Worker[] workers = new Worker[numThreads];
        private ConcurrentBag<WorkInfo> queue = new ConcurrentBag<WorkInfo>();

        public WorkQueue ()
        {

            for (var i = 0; i < numThreads; i++)
            {
                workers[i] = new Worker(queue);
                threads[i] = new Thread(workers[i].run);
                threads[i].Start();
            }
        }

        public bool addWork(LayerModel.RenderDelegate renderDelegate, RenderInfo info)
        {
            queue.Add(new WorkInfo ( renderDelegate, info ));
            return true;
        }

        public bool addWork(LayerModel.RenderDelegate renderDelegate, List<RenderInfo> infos)
        {
            foreach (var worker in workers)
            {
                worker.wait = true;
            }
            foreach (var info in infos)
            {
                queue.Add(new WorkInfo(renderDelegate, info));
            }
            foreach (var worker in workers)
            {
                worker.wait = false;
            }
            return true;
        }

        public void cancel()
        {
            foreach (var worker in workers)
            {
                worker.cancelWork = true;
            }
            queue.Clear();
        }
        public void close()
        {
            foreach (var worker in workers)
            {
                worker.abortWork = true;
            }
            queue.Clear();
        }
    }
}

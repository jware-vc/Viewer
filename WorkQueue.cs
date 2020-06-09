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
        private ConcurrentQueue<Render> renderQueue;
        private RandUtil rand;

        public Worker (ConcurrentQueue<Render> _renderQueue, int seed=0)
        {
            renderQueue = _renderQueue;
            rand = new RandUtil(seed);
        }
        public void Run()
        {

            while (abortWork != true)
            {
                Render ren;
                if (renderQueue.TryDequeue(out ren))
                {
                    var result = ren(in rand, in cancelWork);
                    if (cancelWork)
                    {
                        cancelWork = false;
                    }
                    renderQueue.Enqueue(ren);
                }
                Thread.Sleep(10);
            }
        }

    }
    public class WorkQueue
    {
        private static int numThreads = Environment.ProcessorCount;
        private Thread[] threads = new Thread[numThreads];
        private Worker[] workers = new Worker[numThreads];
        private ConcurrentQueue<Render> queue = new ConcurrentQueue<Render>();

        public WorkQueue ()
        {

            for (var i = 0; i < numThreads; i++)
            {
                workers[i] = new Worker(queue, i*309830);
                threads[i] = new Thread(workers[i].Run);
                threads[i].Start();
            }
        }

        public bool AddWork(Render info)
        {
            queue.Enqueue(info);
            return true;
        }

        public bool AddWork(Render[] infos)
        {
            foreach (var info in infos) AddWork(info); 
            return true;
        }
        public void Clear()
        {
            queue.Clear();
        }

        public void Cancel()
        {
            foreach (var worker in workers) worker.cancelWork = true;
        }
        public void Close()
        {
            foreach (var worker in workers) worker.abortWork = true;
        }
    }
}

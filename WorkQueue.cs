using Raytracer;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace GradientView
{
    class Worker
    {
        public bool cancelWork = false;
        public bool abortWork = false;
        private readonly RandUtil rand;

        public Worker(ConcurrentQueue<Render> _renderQueue, int seed = 0)
        {
            RenderQueue = _renderQueue;
            rand = new RandUtil(seed);
        }

        public ConcurrentQueue<Render> RenderQueue { get; set; }

        public void Run()
        {

            while (abortWork != true)
            {
                if (RenderQueue.TryDequeue(out Render ren))
                {
                    var needsWork = ren(in rand, in cancelWork);
                    if (cancelWork)
                    {
                        cancelWork = false;
                    }
                    if (needsWork)
                    {
                        RenderQueue.Enqueue(ren);
                    }
                }
                Thread.Sleep(1);
            }
        }

    }
    public class WorkQueue
    {
        private static readonly int numThreads = Environment.ProcessorCount;
        private readonly Thread[] threads = new Thread[numThreads];
        private readonly Worker[] workers = new Worker[numThreads];
        private readonly ConcurrentQueue<Render> queue = new ConcurrentQueue<Render>();

        public WorkQueue()
        {

            for (var i = 0; i < numThreads; i++)
            {
                workers[i] = new Worker(queue, i * 309830);
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
            Debug.WriteLine("Adding Work!");
            foreach (var info in infos)
            {
                AddWork(info);
            }

            return true;
        }
        public void Clear()
        {
            queue.Clear();
        }

        public void Cancel()
        {
            foreach (var worker in workers)
            {
                worker.cancelWork = true;
            }
        }
        public void Close()
        {
            foreach (var worker in workers)
            {
                worker.abortWork = true;
            }
        }
    }
}

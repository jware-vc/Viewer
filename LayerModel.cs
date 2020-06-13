using Raytracer;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading;

namespace GradientView
{
    public delegate Vector3 RenderDelegate(in Ray r, in Hitable hitable, in int depth, in RandUtil rand);
    public delegate bool Render(in RandUtil rand, in bool cancelWork);
    public class LayerModel
    {

        public Layer layer;
        public Int32Rect updateRect;
        public LayerModel() { }
        public Camera cam;


        public Layer AddFrame(string layerName, Program _prog, RenderDelegate render, int width, int height, int numSamples, int bpp)
        {

            layer = new Layer(render, _prog, width, height, numSamples, bpp);
            updateRect = new Int32Rect(0, 0, width, height);
            Console.WriteLine($"Adding Layer: {layerName}");
            return layer;
        }




        public void updateBpm(WriteableBitmap bpm)
        {
            bpm.Lock();
            Marshal.Copy(layer.pix, 0, bpm.BackBuffer, layer.pix.Length);
            bpm.AddDirtyRect(updateRect);
            bpm.Unlock();
        }


        public static uint clamp(in uint _min, in uint _val, in uint _max)
        {
            if (_val < _min) return _min;
            if (_val > _max) return _max;
            return _val;
        }

        public static int clamp(in int _min, in int _val, in int _max)
        {
            if (_val < _min) return _min;
            if (_val > _max) return _max;
            return _val;
        }

        public static float clamp(in float _min, in float _val, in float _max)
        {
            if (_val < _min) return _min;
            if (_val > _max) return _max;
            return _val;
        }

        public static IEnumerable<(int, int)> getAA(int aa)
        {

            for (var a1 = 0; a1 < aa; a1++)
            {
                for (var a2 = 0; a2 < aa; a2++)
                {
                    yield return (a1, a2);
                }
            }
        }

        public static IEnumerable<(int, int, int)> GetRange(Region reg)
        {
#if false
            int sampleSize;
            if (reg.sampleIndex < 10)
            {
                sampleSize = 8;
            }
            else if (reg.sampleIndex < 20)
            {
                sampleSize = 4;
            }
            else
            {
                sampleSize = 2;
            }
#else
            var sampleSize = 1;
#endif
            for (int j = reg.yEnd; j > reg.yStart; j -= sampleSize)
            {
                for (int i = reg.xStart; i < reg.xEnd; i += sampleSize)
                {
                    yield return (i, j, sampleSize);
                }
            }
        }

        public static bool RenderProgressive(Region region, ref Layer layer, in RandUtil rand, in bool cancelWork)
        {
            Vector3 tmpCol, disk;
            Ray ray;
            float u, v;
            int idx;
            foreach ((var i, var j, var sampleSize) in GetRange(region))
            {
                if (cancelWork)
                {
                    splatColor(region.xStart, region.yStart, region.width, new Vector3(0), ref region, ref layer);
                    return region.sampleIndex <= layer.numSamples;
                }
                else if (region.sampleIndex == layer.numSamples)
                {
                    return true;
                }
                else
                {
                    idx = (j * region.width) + i;
                    tmpCol = layer.data[idx];
                    disk = rand.random_in_unit_disk();
                    u = (float)(i + disk.X) / (float)region.width;
                    v = (float)(j + disk.Y) / (float)region.height;
                    ray = layer.prog.cam.GetRay(u, v, in rand);
                    ray.point_at_parameter(2.0f);
                    tmpCol += layer.render(ray, layer.prog.world, 0, in rand);
                }
                splatColor(i, j, sampleSize, tmpCol, ref region, ref layer);
            }
            Interlocked.Increment(ref region.sampleIndex);
            //return true;
            return region.sampleIndex <= layer.numSamples;
        }
        public static void splatColor(int i, int j, int sampleSize, Vector3 col, ref Region region, ref Layer layer)
        {
            var sampCol = Vector3.SquareRoot(col / (region.sampleIndex + 1));
            for (var _j = j; _j < j + sampleSize; _j++)
            { 
                for (var _i = i; _i < i + sampleSize; _i++)
                {
                    var idx = (_j * region.width) + _i;
                    if (idx < layer.data.Length)
                    {
                        layer.data[idx] = col;
                        var destIdx = (_j * (region.width * 4)) + (_i * 4);
                        layer.pix[destIdx + 0] = (byte)clamp(0, (int)(sampCol.Z * 255.99f), 255);
                        layer.pix[destIdx + 1] = (byte)clamp(0, (int)(sampCol.Y * 255.99f), 255);
                        layer.pix[destIdx + 2] = (byte)clamp(0, (int)(sampCol.X * 255.99f), 255);
                        layer.pix[destIdx + 3] = (byte)(255);
                    }
                }
            }
        }

    }

    public class Layer
    {
        public byte[] pix;
        public Vector3[] data;
        public Render[] renders;
        public Region[] regions;
        public RenderDelegate render;
        public int numSamples;
        public int bpm, width, height;
        public Program prog;
        public Layer (RenderDelegate _render, Program _prog, int _width, int _height, int _numSamples, int _bpm)
        {
            render = _render;
            width = _width;
            height = _height;
            bpm = _bpm;
            pix = new byte[width * height * 4];
            data = new Vector3[width * height];
            prog = _prog;
            numSamples = _numSamples;
            regions = GetRegions(width, height, bpm);
            renders = GetRenders();
        }
        public bool Reset()
        {
            //pix = new byte[width * height * 4];
            //data = new Vector3[width * height];
            //renders = GetRenders();
            var empty = new Vector3(0);
            for (var i = 0; i < pix.Length; i += 4)
            {
                pix[i] = 0;
                pix[i+1] = 0;
                pix[i+2] = 0;
                pix[i+3] = 255;
            }
            for (var i = 0; i < data.Length; i++) data[i] = empty;
            for (var i = 0; i < regions.Length; i++)
            {
                Interlocked.Exchange(ref regions[i].sampleIndex, 0);
            }
            return true;
        }
        public Render[] GetRenders()
        {
            var renders = new List<Render>();
            var layer = this;
            foreach (var reg in regions) 
            {
                renders.Add((in RandUtil rand, in bool cancel) => {
                    var result = LayerModel.RenderProgressive(reg, ref layer, in rand, in cancel);
                    return result;
                });
            }
            return renders.ToArray();
        }
        private static Region[] GetRegions(int width, int height, int bpm)
        {
            int xChunkSize = (int)(width / 8);
            int yChunkSize = (int)(height / 8);
            var reg = new List<Region>();
            for (int endY = height - 1; endY >= 0; endY -= yChunkSize)
            {
                var startY = endY - yChunkSize > 0 ? endY - yChunkSize : 0;
                for (int startX = 0; startX < width; startX += xChunkSize)
                {
                    var endX = startX + xChunkSize > width ? width : startX + xChunkSize;
                    reg.Add(new Region(startX, endX, startY, endY, width, height, bpm));
                }
            }
            return reg.ToArray();
        }
    }

    public class Region
    {
        public Region(int _xStart, int _xEnd, int _yStart, int _yEnd, int _width, int _height, int bitsPerPixel)
        {
            xStart = _xStart;
            xEnd = _xEnd;
            yStart = _yStart;
            yEnd = _yEnd;
            sectionHeight = _yEnd - _yStart;
            sectionWidth = _xEnd - _xStart;
            height = _height;
            width = _width;
            sampleIndex = 0;
            aa = 8; 
            stride = ((sectionWidth * bitsPerPixel + 7) / 8);
        }
        public int xStart, xEnd, yStart, yEnd, sectionHeight, sectionWidth, height, width, stride, sampleIndex, aa;
    }


}

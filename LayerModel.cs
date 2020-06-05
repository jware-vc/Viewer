using Raytracer;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Security.Permissions;
using System.Diagnostics;

namespace GradientView
{
    public class LayerModel
    {
        public delegate Vector3 RenderDelegate(in Ray r, in Hitable hitable, in int depth);

        private Dictionary<string, Program> programs = new Dictionary<string, Program>();
        public Dictionary<string, RenderDelegate> renderFuncs = new Dictionary<string, RenderDelegate>();
        public Dictionary<string, WriteableBitmap> bitmaps = new Dictionary<string, WriteableBitmap>();
        public Dictionary<string, byte[]> pixels = new Dictionary<string, byte[]>();
        public Dictionary<string, List<RenderInfo>> taskChunks = new Dictionary<string, List<RenderInfo>>();

        public LayerModel() { }
        public Camera cam;


        public bool canRender()
        {
            return taskChunks.Any();
        }

        public void addLayer(string layerName, Program _prog, RenderDelegate render, int width, int height, int numSamples)
        {
            programs[layerName] = _prog;
            renderFuncs[layerName] = render;
#if true
            bitmaps[layerName] = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            pixels[layerName] = new byte[width * height * 4];
#else
            bitmaps[layerName] = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgba64, null);
#endif
            taskChunks[layerName] = genChunkInfo(layerName, _prog, width, height, numSamples);
            Console.WriteLine($"Adding Layer: {layerName}");
        }

        public List<RenderInfo> genChunkInfo(string layerName, Program prog, int width, int height, int numSamples)
        {
            var infoList = new List<RenderInfo>();
            cam = new Camera(prog.lookFrom, prog.lookAt, new Vector3(0, 1, 0), prog.vfov, width / height, prog.aperature, prog.dist_to_focus, 0, 1);
            int xChunkSize = (int)(width / 8);
            int yChunkSize = (int)(height / 8);
            var bpm = bitmaps[layerName];
            var pix = pixels[layerName];
            for (int endY = height - 1; endY >= 0; endY -= yChunkSize)
            {
                var startY = endY - yChunkSize > 0 ? endY - yChunkSize : 0;
                for (int startX = 0; startX < width; startX += xChunkSize)
                {
                    var endX = startX + xChunkSize > width ? width : startX + xChunkSize;
                    var reg = new Region(startX, endX, startY, endY, width, height, numSamples, bpm.Format.BitsPerPixel);
                    infoList.Add(new RenderInfo(layerName, ref reg, ref prog.world, ref cam, ref bpm, ref pix));
                }
            }
            return infoList;
        }


        public void updateBpm(string layerName)
        {
            var bpm = bitmaps[layerName];
            bpm.Lock();
            var layer = pixels[layerName];
            Marshal.Copy(layer, 0, bpm.BackBuffer, layer.Length);
            bpm.AddDirtyRect(new Int32Rect(0, 0, bpm.PixelWidth - 1, bpm.PixelHeight - 1));
            bpm.Unlock();
        }

        public void updateBitmap(RenderInfo info)
        {
            var stride = ((info.region.sectionWidth * info.bitmap.Format.BitsPerPixel + 7) / 8);
            info.bitmap.WritePixels(new Int32Rect(info.region.xStart, info.region.yStart, info.region.sectionWidth, info.region.sectionHeight), info.pixels, stride, 0);
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

        public static RenderInfo Render2(RenderDelegate render, RenderInfo info)
        {
            var disk = new Vector3(0.5f, 0.5f, 0f);
            for (int j = info.region.yStart; j < info.region.yEnd + 1; j++)
            {
                for (int i = info.region.xStart; i < info.region.xEnd; i++)
                {
                    var tmpCol = new Vector3(0, 0, 0);
                    for (int samp = 0; samp < info.region.numSamples; samp++)
                    {
                        //var disk = Program.randUtil.random_in_unit_disk();
                        var u = (float)(i + disk.X) / (float)info.region.width;
                        var v = (float)(j + disk.Y) / (float)info.region.height;
                        var r = info.cam.getRay(u, v);
                        r.point_at_parameter(2.0f);
                        tmpCol += render(r, info.world, 0);
                    }
                    tmpCol /= info.region.numSamples;
                    tmpCol = Vector3.SquareRoot(tmpCol);
                    var destIdx = (j * (info.region.width * 4)) + (i * 4);
                    info.pix[destIdx++] = (byte)clamp(0, (int)(tmpCol.Z * 255.99f), 255);
                    info.pix[destIdx++] = (byte)clamp(0, (int)(tmpCol.Y * 255.99f), 255);
                    info.pix[destIdx++] = (byte)clamp(0, (int)(tmpCol.X * 255.99f), 255);
                    info.pix[destIdx++] = (byte)(255);
                }
            }
            return info;
        }

        public static RenderInfo Render4(RenderDelegate render, RenderInfo info, in bool cancelWork)
        {
            var disk = new Vector3(0.5f, 0.5f, 0f);
            for (int j = info.region.yStart; j < info.region.yEnd + 1; j++)
            {
                for (int i = info.region.xStart; i < info.region.xEnd; i++)
                {
                    var tmpCol = new Vector3(0, 0, 0);
                    for (int samp = 0; samp < info.region.numSamples; samp++)
                    {
                        //var disk = Program.randUtil.random_in_unit_disk();
                        var u = (float)(i + disk.X) / (float)info.region.width;
                        var v = (float)(j + disk.Y) / (float)info.region.height;
                        var r = info.cam.getRay(u, v);
                        r.point_at_parameter(2.0f);
                        tmpCol += render(r, info.world, 0);
                    }
                    tmpCol /= info.region.numSamples;
                    tmpCol = Vector3.SquareRoot(tmpCol);
                    var destIdx = (j * (info.region.width * 4)) + (i * 4);
                    info.pix[destIdx++] = (byte)clamp(0, (int)(tmpCol.Z * 255.99f), 255);
                    info.pix[destIdx++] = (byte)clamp(0, (int)(tmpCol.Y * 255.99f), 255);
                    info.pix[destIdx++] = (byte)clamp(0, (int)(tmpCol.X * 255.99f), 255);
                    info.pix[destIdx++] = (byte)(255);
                    if (cancelWork)
                    {
                        Debug.Write("Cancelling early!");
                        return info;
                    }
                }
            }
            return info;
        }

        public static RenderInfo Render3(RenderDelegate render, RenderInfo info, CancellationToken cancellationToken)
        {
            var disk = new Vector3(0.5f, 0.5f, 0f);
            for (int j = info.region.yStart; j < info.region.yEnd + 1; j++)
            {
                for (int i = info.region.xStart; i < info.region.xEnd; i++)
                {
                    var tmpCol = new Vector3(0, 0, 0);
                    for (int samp = 0; samp < info.region.numSamples; samp++)
                    {
                        //var disk = Program.randUtil.random_in_unit_disk();
                        var u = (float)(i + disk.X) / (float)info.region.width;
                        var v = (float)(j + disk.Y) / (float)info.region.height;
                        var r = info.cam.getRay(u, v);
                        r.point_at_parameter(2.0f);
                        tmpCol += render(r, info.world, 0);
                    }
                    tmpCol /= info.region.numSamples;
                    tmpCol = Vector3.SquareRoot(tmpCol);
                    var destIdx = (j * (info.region.width * 4)) + (i * 4);
                    info.pix[destIdx++] = (byte)clamp(0, (int)(tmpCol.Z * 255.99f), 255);
                    info.pix[destIdx++] = (byte)clamp(0, (int)(tmpCol.Y * 255.99f), 255);
                    info.pix[destIdx++] = (byte)clamp(0, (int)(tmpCol.X * 255.99f), 255);
                    info.pix[destIdx++] = (byte)(255);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            return info;
        }
    }

    public readonly struct Region
    {
        public Region(int _xStart, int _xEnd, int _yStart, int _yEnd, int _width, int _height, int _samples, int bitsPerPixel)
        {
            xStart = _xStart;
            xEnd = _xEnd;
            yStart = _yStart;
            yEnd = _yEnd;
            sectionHeight = _yEnd - _yStart;
            sectionWidth = _xEnd - _xStart;
            height = _height;
            width = _width;
            numSamples = _samples;
            stride = ((sectionWidth * bitsPerPixel + 7) / 8);
        }
        public readonly int xStart, xEnd, yStart, yEnd, sectionHeight, sectionWidth, height, width, numSamples, stride;
    }


    public struct RenderInfo
    {
        public RenderInfo(string _layer, ref Region _region, ref Hitable _world, ref Camera _cam, ref WriteableBitmap _bitmap, ref byte[] _pix)
        {
            layer = _layer;
            region = _region;
            world = _world;
            cam = _cam;
            var numChannels = 3;
            pixels = new byte[region.sectionWidth * region.sectionHeight * numChannels];
            pix = _pix;
            bitmap = _bitmap;
        }
        public string layer;
        public Hitable world;
        public Camera cam;
        public Region region;
        public byte[] pixels;
        public byte[] pix;
        public WriteableBitmap bitmap;
    }
}

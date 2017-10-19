using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitMiracle.LibTiff.Classic;
using Warp;
using Warp.Tools;

namespace stacker
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Number of frames:");
            string FramesString = Console.ReadLine();
            int NFrames;
            try
            {
                NFrames = int.Parse(FramesString);
                Console.WriteLine("Using " + NFrames + " frames.");
            }
            catch (Exception)
            {
                return;
            }

            Console.WriteLine("Path to frames:");
            string FolderPath = Console.ReadLine();
            if (FolderPath[FolderPath.Length - 1] != '\\')
                FolderPath += "\\";
            Console.WriteLine("Reading frames from " + FolderPath);
            Console.WriteLine("");

            Directory.CreateDirectory(FolderPath + "stacked");

            Console.WriteLine("Delete original files after completion? (y/n)");
            bool DeleteWhenDone = Console.ReadLine().ToLower().Contains("y");
            Console.WriteLine($"Original files will {(DeleteWhenDone? "" : "not ")}be deleted.");
            if (!DeleteWhenDone)
                Directory.CreateDirectory(FolderPath + "original");

            #region Gain reference

            Console.WriteLine("Path to gain reference:");
            string GainPath = Console.ReadLine();
            Image Gain = null;
            float[] GainData = null;
            bool CheckGainMismatch = false;

            if (!string.IsNullOrEmpty(GainPath))
            {
                try
                {
                    Gain = Image.FromFile(GainPath, -1, true);
                }
                catch
                {
                    Gain = Image.FromFile(GainPath, -1, false);
                }

                // Can't use GPU ;-)
                {
                    GainData = Gain.GetHost(Intent.ReadWrite)[0];
                    for (int i = 0; i < GainData.Length; i++)
                        GainData[i] = 1f / Math.Max(0.0001f, GainData[i]);
                }

                Console.WriteLine("Using gain reference from " + GainPath);
                Console.WriteLine("");

                Console.WriteLine("Check for gain reference mismatch? (y/n)");
                CheckGainMismatch = Console.ReadLine().ToLower().Contains("y");

                Console.WriteLine("Transpose gain reference? (y/n)");
                bool Transpose = Console.ReadLine().ToLower().Contains("y");

                Console.WriteLine("Flip X in gain reference? (y/n)");
                bool FlipX = Console.ReadLine().ToLower().Contains("y");

                Console.WriteLine("Flip Y in gain reference? (y/n)");
                bool FlipY = Console.ReadLine().ToLower().Contains("y");

                if (Transpose)
                    Console.WriteLine("Gain will be transposed.");
                if (FlipX)
                    Console.WriteLine("Gain will be flipped in the (new, if transposed) X dimension.");
                if (FlipY)
                    Console.WriteLine("Gain will be flipped in the (new, if transposed) Y dimension.");

                if (Transpose)
                {
                    GainData = Gain.GetHost(Intent.Read)[0];
                    float[] TransposedData = new float[GainData.Length];
                    for (int y = 0; y < Gain.Dims.Y; y++)
                        for (int x = 0; x < Gain.Dims.X; x++)
                            TransposedData[x * Gain.Dims.Y + y] = GainData[y * Gain.Dims.X + x];

                    Image GainTrans = new Image(TransposedData, new int3(Gain.Dims.Y, Gain.Dims.X, 1));
                    Gain.Dispose();
                    Gain = GainTrans;
                }

                if (FlipX)
                {
                    GainData = Gain.GetHost(Intent.Read)[0];
                    float[] FlipXData = new float[GainData.Length];
                    for (int y = 0; y < Gain.Dims.Y; y++)
                        for (int x = 0; x < Gain.Dims.X; x++)
                            FlipXData[y * Gain.Dims.X + (Gain.Dims.X - 1 - x)] = GainData[y * Gain.Dims.X + x];

                    Image GainFlipX = new Image(FlipXData, new int3(Gain.Dims.X, Gain.Dims.Y, 1));
                    Gain.Dispose();
                    Gain = GainFlipX;
                }

                if (FlipY)
                {
                    GainData = Gain.GetHost(Intent.Read)[0];
                    float[] FlipYData = new float[GainData.Length];
                    for (int y = 0; y < Gain.Dims.Y; y++)
                        for (int x = 0; x < Gain.Dims.X; x++)
                            FlipYData[(Gain.Dims.Y - 1 - y) * Gain.Dims.X + x] = GainData[y * Gain.Dims.X + x];

                    Image GainFlipY = new Image(FlipYData, new int3(Gain.Dims.X, Gain.Dims.Y, 1));
                    Gain.Dispose();
                    Gain = GainFlipY;
                }

                GainData = Gain.GetHost(Intent.Read)[0];
            }

            #endregion

            while (true)
            {
                List<string>[] FrameNames = new List<string>[NFrames];
                for (int n = 0; n < NFrames; n++)
                    FrameNames[n] = new List<string>();
                List<string>[] CleanFrameNames = new List<string>[NFrames];
                for (int n = 0; n < NFrames; n++)
                    CleanFrameNames[n] = new List<string>();

                for (int n = 0; n < NFrames; n++)
                    foreach (var filename in Directory.EnumerateFiles(FolderPath, "*-" + (n + 1).ToString("D4") + ".mrc"))
                        FrameNames[n].Add(filename);

                int NFiles = FrameNames[0].Count;

                for (int f = 0; f < NFiles; f++)
                {
                    FileInfo Info = new FileInfo(FrameNames[0][f]);
                    string RootName = Info.Name.Substring(0, Info.Name.Length - "-0000.mrc".Length);
                    bool IsValid = true;
                    for (int n = 0; n < NFrames; n++)
                        if (!FrameNames[n].Any(v => v.Contains(RootName)))
                            IsValid = false;
                    if (!IsValid)
                        continue;

                    for (int n = 0; n < NFrames; n++)
                        CleanFrameNames[n].Add(FrameNames[n].First(v => v.Contains(RootName)));
                }

                FrameNames = CleanFrameNames;

                NFiles = FrameNames[0].Count;

                if (NFiles == 0)
                {
                    Thread.Sleep(5000);
                    continue;
                }

                Console.WriteLine("Found " + NFiles + " new stacks.");
                
                Thread.Sleep(5000);

                bool StillWriting = false;

                for (int f = 0; f < NFiles; f++)
                {
                    Image Frame0;
                    try
                    {
                        Frame0 = Image.FromFile(FrameNames[0][f], -1, true);
                    }
                    catch
                    {
                        Frame0 = Image.FromFile(FrameNames[0][f], -1, false);
                    }
                    Console.Write(".");
                    
                    byte[][] StackData = Helper.ArrayOfFunction(i => new byte[Frame0.ElementsSliceReal], NFrames);

                    float[] Frame0Data = Frame0.GetHost(Intent.Read)[0];
                    if (Gain == null)
                        for (int i = 0; i < Frame0Data.Length; i++)
                            StackData[0][i] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(Frame0Data[i])));
                    else
                        for (int i = 0; i < Frame0Data.Length; i++)
                            StackData[0][i] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(Frame0Data[i] * GainData[i])));
                    Frame0.Dispose();

                    if (CheckGainMismatch && Gain != null)
                    {
                        double Mismatch = 0;
                        for (int i = 0; i < Frame0Data.Length; i++)
                            Mismatch += Math.Abs(Frame0Data[i] * GainData[i] - Math.Round(Frame0Data[i] * GainData[i]));
                        Mismatch /= Frame0Data.Length;
                        if (Mismatch > 0.01)
                            throw new Exception("Dividing by gain reference does not produce integers. Fix your reference, or turn off this error check.");
                    }

                    for (int n = 1; n < NFrames; n++)
                    {
                        Image Frame;
                        try
                        {
                            Frame = Image.FromFile(FrameNames[n][f], -1, true);
                        }
                        catch
                        {
                            Frame = Image.FromFile(FrameNames[n][f], -1, false);
                        }

                        float[] FrameData = Frame.GetHost(Intent.Read)[0];
                        if (Gain == null)
                            for (int i = 0; i < Frame0Data.Length; i++)
                                StackData[n][i] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(FrameData[i])));
                        else
                            for (int i = 0; i < Frame0Data.Length; i++)
                                StackData[n][i] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(FrameData[i] * GainData[i])));
                        Frame.Dispose();

                        Console.Write(".");
                    }
                    Console.WriteLine("");

                    FileInfo Info = new FileInfo(FrameNames[0][f]);
                    string RootName = Info.Name.Substring(0, Info.Name.Length - "-0000.mrc".Length);

                    Thread WriteThread = new Thread(() =>
                    {
                        while (StillWriting)
                            Thread.Sleep(1000);

                        StillWriting = true;

                        try
                        {
                            using (Tiff output = Tiff.Open(FolderPath + "stacked\\" + RootName + ".tif", "w"))
                            {
                                int width = Frame0.Dims.X;
                                int height = Frame0.Dims.Y;
                                const int samplesPerPixel = 1;
                                const int bitsPerSample = 8;

                                for (int page = 0; page < NFrames; page++)
                                {
                                    StillWriting = true;

                                    output.SetField(TiffTag.IMAGEWIDTH, width / samplesPerPixel);
                                    output.SetField(TiffTag.IMAGELENGTH, height);
                                    output.SetField(TiffTag.SAMPLESPERPIXEL, samplesPerPixel);
                                    output.SetField(TiffTag.BITSPERSAMPLE, bitsPerSample);
                                    output.SetField(TiffTag.ORIENTATION, Orientation.BOTLEFT);
                                    output.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);

                                    output.SetField(TiffTag.COMPRESSION, Compression.LZW);

                                    output.SetField(TiffTag.ROWSPERSTRIP, output.DefaultStripSize(0));
                                    output.SetField(TiffTag.XRESOLUTION, 100.0);
                                    output.SetField(TiffTag.YRESOLUTION, 100.0);
                                    output.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);

                                    // specify that it's a page within the multipage file
                                    output.SetField(TiffTag.SUBFILETYPE, FileType.PAGE);
                                    // specify the page number
                                    output.SetField(TiffTag.PAGENUMBER, page, NFrames);

                                    for (int j = 0; j < height; j++)
                                        output.WriteScanline(Helper.Subset(StackData[page], j * width, (j + 1) * width), j);

                                    output.WriteDirectory();
                                    output.FlushData();
                                }
                            }

                            for (int n = 0; n < NFrames; n++)
                                if (DeleteWhenDone)
                                    File.Delete(FrameNames[n].First(v => v.Contains(RootName)));
                                else
                                    File.Move(FrameNames[n].First(v => v.Contains(RootName)), FolderPath + "original/" + Helper.PathToNameWithExtension(FrameNames[n].First(v => v.Contains(RootName))));

                            StillWriting = true;
                        }
                        catch (Exception exc)
                        {
                            Console.WriteLine("ERROR: Could not write " + RootName);
                            Console.WriteLine(exc);
                        }

                        StillWriting = false;
                    });
                    WriteThread.Start();

                    Console.WriteLine("Done: " + RootName);
                }

                Thread.Sleep(1000);

                while (StillWriting)
                    Thread.Sleep(1000);
            }
        }
    }
}

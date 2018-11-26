using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitMiracle.LibTiff.Classic;
using Warp;
using Warp.Headers;
using Warp.Tools;

namespace stacker2
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Path to stacks:");
            string FolderPath = Console.ReadLine();
            if (FolderPath[FolderPath.Length - 1] != '\\' || FolderPath[FolderPath.Length - 1] != '/')
                FolderPath += "\\";
            Console.WriteLine("Reading stacks from " + FolderPath + "\n");

            Console.WriteLine("Look for files recursively in subfolders? (y/n)");
            bool DoRecursiveSearch = Console.ReadLine().ToLower().Contains("y");
            Console.WriteLine($"Files will {(DoRecursiveSearch ? "" : "not ")}be searched recursively.\n");

            Console.WriteLine("Output folder (leave empty if identical with input):");
            string OutputPath = Console.ReadLine();
            if (string.IsNullOrEmpty(OutputPath))
                OutputPath = FolderPath;
            if (OutputPath[OutputPath.Length - 1] != '\\' || OutputPath[OutputPath.Length - 1] != '/')
                OutputPath += "\\";
            Console.WriteLine("Writing compressed stacks to " + OutputPath + "\n");

            Console.WriteLine("Compress to TIFF (c), or just move (m)? (c/m)");
            bool Compress = Console.ReadLine().ToLower().Contains("c");
            Console.WriteLine($"Files will be {(Compress ? "written as compressed TIFFs" : "just moved")}.\n");

            string Extension = "mrc";
            if (!Compress)
            {
                Console.WriteLine("What is the input file extension?");
                Extension = Console.ReadLine().ToLower();
                if (Extension[0] == '*')
                    Extension = Extension.Substring(1);
                if (Extension[0] == '.')
                    Extension = Extension.Substring(1);

                Console.WriteLine($"Using *.{Extension} as input file extension.\n");
            }

            Console.WriteLine("Number of frames:");
            string FramesString = Console.ReadLine();
            int NFrames;
            try
            {
                NFrames = int.Parse(FramesString);
                Console.WriteLine("Using " + NFrames + " frames.\n");
            }
            catch (Exception)
            {
                return;
            }

            Console.WriteLine("Delete original files after completion? (y/n)");
            bool DeleteWhenDone = Console.ReadLine().ToLower().Contains("y");
            Console.WriteLine($"Original files will {(DeleteWhenDone? "" : "not ")}be deleted.\n");
            if (!DeleteWhenDone)
                Directory.CreateDirectory(FolderPath + "original");

            Console.WriteLine("Delete superfluous gain references? (y/n)");
            bool DeleteExtraGain = Console.ReadLine().ToLower().Contains("y");
            Console.WriteLine($"Superfluous gain references will {(DeleteWhenDone ? "" : "not ")}be deleted.\n");
            if (!DeleteWhenDone)
                Directory.CreateDirectory(FolderPath + "original");

            Console.WriteLine("Number of stacks to be processed in parallel (4 is a good value):");
            int NParallel = int.Parse(Console.ReadLine());
            Console.WriteLine($"{NParallel} stacks will be processed in parallel.\n");

            List<string> HaveBeenProcessed = new List<string>();

            while (true)
            {
                List<string> FrameNames = new List<string>();
                List<string> GainRefNames = new List<string>();

                foreach (var filename in Directory.EnumerateFiles(FolderPath, "*." + Extension,
                                                                  DoRecursiveSearch ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        MapHeader Header = MapHeader.ReadFromFile(filename);
                        if (Header.Dimensions.Z != NFrames)
                        {
                            if (Header.Dimensions.Z == 1)
                                GainRefNames.Add(filename);
                            continue;
                        }

                        if (HaveBeenProcessed.Contains(filename))
                            continue;

                        if (DeleteWhenDone)
                            FrameNames.Add(filename);
                        else if (!filename.Contains("\\original\\"))
                            FrameNames.Add(filename);
                    }
                    catch
                    {
                    }
                }

                int NFiles = FrameNames.Count;

                if (NFiles == 0)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                Console.WriteLine("Found " + NFiles + " new stacks.");
                
                Thread.Sleep(1000);
                
                SemaphoreSlim WritingSemaphore = new SemaphoreSlim(NParallel);

                for (int f = 0; f < NFiles; f++)
                {
                    string FrameName = FrameNames[f];

                    MapHeader Header = MapHeader.ReadFromFilePatient(500, 100, FrameName, new int2(1), 0, typeof(float));

                    if (Compress)
                    {
                        Image StackOut = new Image(Header.Dimensions);
                        float[][] StackOutData = StackOut.GetHost(Intent.Read);

                        for (int n = 0; n < NFrames; n++)
                        {
                            Image Frame = Image.FromFilePatient(50, 100, FrameName, n);

                            float[] FrameData = Frame.GetHost(Intent.Read)[0];
                            if (Compress)
                                for (int i = 0; i < FrameData.Length; i++)
                                    StackOutData[n][i] = (float)Math.Max(0, Math.Min(255, Math.Round(FrameData[i])));
                            else
                                for (int i = 0; i < FrameData.Length; i++)
                                    StackOutData[n][i] = FrameData[i];
                            Frame.Dispose();

                            Console.Write(".");
                        }

                        Console.WriteLine("");

                        HaveBeenProcessed.Add(FrameName);

                        string RootName = Helper.PathToName(FrameName);

                        Thread WriteThread = new Thread(() =>
                        {

                            try
                            {
                                //if (Compress)
                                    StackOut.WriteTIFF(OutputPath + RootName + ".tif", 1, typeof(byte));
                                //else
                                //    StackOut.WriteMRC(OutputPath + RootName + ".mrc", 1, true);

                                if (DeleteWhenDone)
                                    File.Delete(FrameName);
                                else
                                    File.Move(FrameName, FolderPath + "original/" + Helper.PathToNameWithExtension(FrameName));
                            }
                            catch (Exception exc)
                            {
                                Console.WriteLine("ERROR: Could not write " + RootName);
                                Console.WriteLine(exc);
                                HaveBeenProcessed.Remove(FrameName);
                            }

                            WritingSemaphore.Release();
                        });

                        while (WritingSemaphore.CurrentCount < 1)
                            Thread.Sleep(100);

                        WritingSemaphore.Wait();
                        WriteThread.Start();

                        Console.WriteLine("Done reading: " + RootName);
                    }
                    else
                    {
                        bool Success = false;

                        while (!Success)
                        {
                            try
                            {
                                string NameOut = OutputPath + Helper.PathToNameWithExtension(FrameName);

                                if (DeleteWhenDone)
                                    File.Move(FrameName, NameOut);
                                else
                                {
                                    File.Copy(FrameName, NameOut);
                                    File.Move(FrameName, FolderPath + "original/" + Helper.PathToNameWithExtension(FrameName));
                                }

                                HaveBeenProcessed.Add(FrameName);
                                Success = true;

                                Console.WriteLine("Done moving: " + Helper.PathToNameWithExtension(FrameName));
                            }
                            catch (Exception exc)
                            {
                                Console.WriteLine("Something went wrong moving " + Helper.PathToNameWithExtension(FrameName) + ":\n" + exc.ToString());
                            }
                        }
                    }
                }

                if (DeleteExtraGain)
                    foreach (var gainRefName in GainRefNames)
                        File.Delete(gainRefName);

                Thread.Sleep(1000);
            }
        }
    }
}

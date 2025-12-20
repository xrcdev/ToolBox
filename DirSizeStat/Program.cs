using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DirSizeStat
{
    internal class Program
    {
        /*
         1. 获取指定目录下(包括子目录)所有文件的大小和数量
         2. 控制台展示,使用递归 |- 代表层级;显示文件夹名称,文件数量,文件大小
         */
        static string _monitorFolder = System.Configuration.ConfigurationManager.AppSettings["MonitorFolder"];
        static string[] _fileExts = System.Configuration.ConfigurationManager.AppSettings["FileExt"].Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        static ConsoleColor _defaultColor = Console.ForegroundColor;
        static long _totalFileSize = 0l;
        static int _totalFileCount = 0;
        static void Main(string[] args)
        {
            var rootDir = new DirectoryInfo(_monitorFolder);
            DisplayDirectoryTree(rootDir, 0);
            //GroupByDay(rootDir);
            //输出 _totalFileCount , _totalFileSize 
            Console.WriteLine($"待处理文件总数量:{_totalFileCount},总大小:{(_totalFileSize / 1024 / 1024.0).ToString("F3").PadLeft(7)} MB");
            Console.ReadLine();
        }

        static void DisplayDirectoryTree(DirectoryInfo dir, int level)
        {
            // 获取当前目录的所有文件
            var files = dir.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                .Where(f => _fileExts.Contains(Path.GetExtension(f.FullName)));
            //.ToList();
            //var maxLength = 0;
            //if (files.Count() > 0)
            //    maxLength = files.Max(f => f.Name.Length);
            // 显示当前目录信息
            Console.Write("|");
            for (int i = 1; i < level; i++)
            {
                Console.Write("  |");
            }
            Console.Write($"- {dir.Name}");


            //从 files 获取最新的文件时间,最旧的文件时间,最大的文件大小,最新的文件大小
            var oldestTime = new DateTime(2025, 1, 1);
            var newestTime = new DateTime(1900, 1, 1);
            var bigest = 0d;
            var smallest = 0d;
            var fileCount = 0;
            var totalFileSize = 0l;
            foreach (var item in files)
            {
                if (newestTime < item.CreationTime)
                {
                    newestTime = item.CreationTime;
                }
                if (oldestTime > item.CreationTime)
                {
                    oldestTime = item.CreationTime;
                }
                if (bigest < item.Length)
                {
                    bigest = item.Length;
                }
                if (smallest > item.Length)
                {
                    smallest = item.Length;
                }
                totalFileSize += item.Length;
                fileCount += 1;
            }

            if (dir.Name.ToCharArray().All(t => char.IsDigit(t)))
            {
                _totalFileCount += fileCount;
                _totalFileSize += totalFileSize;
            }
            //if (oldestTime < new DateTime(2024, 8, 1))
            //{
            Console.ForegroundColor = ConsoleColor.Yellow;
            if (fileCount == 0)
            {
                Console.WriteLine($" 数量: {fileCount.ToString().PadLeft(8)} ");
            }
            else
            {
                Console.Write($" 数量: {fileCount.ToString().PadLeft(8)}, 大小: {(totalFileSize / 1024 / 1024.0).ToString("F3").PadLeft(7)} MB");
                //输出 最新的文件时间,最旧的文件时间,最大的文件大小,最新的文件大小
                Console.WriteLine($" 最新: {newestTime.ToString("yyyy-MM-dd HH:mm:ss")}, 最旧: {oldestTime.ToString("yyyy-MM-dd HH:mm:ss")}, 最大: {(bigest / 1024 / 1024.0).ToString("F3").PadLeft(7)} MB, 最小: {(smallest / 1024 / 1024.0).ToString("F3").PadLeft(7)} MB");
            }

            Console.ForegroundColor = _defaultColor;
            //}
            //else
            //{
            //    Console.WriteLine();
            //}


            // 递归处理子目录
            var subDirs = dir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);
            foreach (var subDir in subDirs)
            {
                DisplayDirectoryTree(subDir, level + 1);
            }
        }

        static void GroupByDay(DirectoryInfo dir)
        {
            // 获取当前目录的所有文件
            var files = dir.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                .Where(f => _fileExts.Contains(Path.GetExtension(f.FullName)));
            //.ToList();

            // 显示当前目录信息

            Console.Write($"- {dir.Name}");
            Console.ForegroundColor = ConsoleColor.Yellow;

            //从 files 获取最新的文件时间,最旧的文件时间,最大的文件大小,最新的文件大小
            var newestTime = DateTime.MinValue;
            var oldestTime = DateTime.Now;
            var bigest = 0d;
            var smallest = 0d;
            var fileCount = 0;
            var totalFileSize = 0l;
            Dictionary<string, int> dicCount = new Dictionary<string, int>();
            Dictionary<string, long> dicSize = new Dictionary<string, long>();
            foreach (var item in files)
            {
                if (newestTime < item.CreationTime)
                {
                    newestTime = item.CreationTime;
                }
                if (oldestTime > item.CreationTime)
                {
                    oldestTime = item.CreationTime;
                }
                if (bigest < item.Length)
                {
                    bigest = item.Length;
                }
                if (smallest > item.Length)
                {
                    smallest = item.Length;
                }
                totalFileSize += item.Length;
                fileCount += 1;
                if (dicCount.ContainsKey(item.CreationTime.ToString("yyyy-MM-dd")))
                {
                    dicCount[item.CreationTime.ToString("yyyy-MM-dd")] += 1;
                }
                else
                {
                    dicCount.Add(item.CreationTime.ToString("yyyy-MM-dd"), 1);
                }
                if (dicSize.ContainsKey(item.CreationTime.ToString("yyyy-MM-dd")))
                {
                    dicSize[item.CreationTime.ToString("yyyy-MM-dd")] += item.Length;
                }
                else
                {
                    dicSize.Add(item.CreationTime.ToString("yyyy-MM-dd"), item.Length);
                }
            }

            Console.Write($" (数量: {fileCount}, 大小: {(totalFileSize / 1024 / 1024.0).ToString("F3")} MB");
            //输出 最新的文件时间,最旧的文件时间,最大的文件大小,最新的文件大小
            Console.WriteLine($" 最新: {newestTime.ToString("yyyy-MM-dd HH:mm:ss")}, 最旧: {oldestTime.ToString("yyyy-MM-dd HH:mm:ss")}, 最大: {(bigest / 1024 / 1024.0).ToString("F3")} MB, 最小: {(smallest / 1024 / 1024.0).ToString("F3")} MB");

            foreach (var item in dicCount.OrderByDescending(kv => kv.Key))
            {
                Console.WriteLine($"日期: {item.Key}, 数量: {item.Value}");
                Console.WriteLine($"日期: {item.Key}, 大小: {(dicSize[item.Key] / 1024 / 1024.0).ToString("F3")} MB");
            }
            //foreach (var item in dicSize)
            //{
            //    Console.WriteLine($"日期: {item.Key}, 大小: {(item.Value / 1024 / 1024.0).ToString("F3")} MB");
            //}
            Console.ForegroundColor = _defaultColor;

            // 递归处理子目录
            //var subDirs = dir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);
            //foreach (var subDir in subDirs)
            //{
            //    DisplayDirectoryTree(subDir, level + 1);
            //}
        }
    }
    //class DirStatInfo
    //{
    //    public int FileCount { get; set; }

    //    /// <summary>
    //    /// unit is mb
    //    /// </summary>
    //    public double DirSize { get; set; }
    //}
}

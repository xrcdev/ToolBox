using CopyFilesConsole.Model;

using Microsoft.Extensions.Configuration;

using Serilog;
using Serilog.Events;

namespace CopyFilesConsole
{
    internal class Program
    {
        static CopyFileConfig _copyFileConfig = new CopyFileConfig();
        static void Main(string[] args)
        {
            //config
            var configuration = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json")
              .Build();
            configuration.Bind("CopyFileConfig", _copyFileConfig);

            //log
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File("log.txt", LogEventLevel.Information)
                .CreateLogger();
            Log.Information("Starting CopyFilesConsole application");

            //toDlls
            var toDlls = Directory.GetFiles(_copyFileConfig.ToDir, "*.dll", SearchOption.AllDirectories);
            Log.Information($"toDlls count:{toDlls.Length}");
            List<CopyFileInfo> toDllInfos = GetInfosByFiles(toDlls, false);

            //fromDlls
            var fromDlls = Directory.GetFiles(_copyFileConfig.FromDir, "*.dll", SearchOption.AllDirectories);
            List<CopyFileInfo> fromDllInfos = GetInfosByFiles(fromDlls, false);
            var dts = fromDllInfos.Select(t => new { t.FileName, t.CreateTime });
            Log.Information($"fromDlls count:{toDlls.Length}");

            Replace(toDllInfos, fromDllInfos);

            //var targExes = Directory.GetFiles(myConfig.CopyDir, "*.exe", SearchOption.AllDirectories);
            //var dlls = Directory.GetFiles(myConfig.FindDir, "*.dll");
            //var exes = Directory.GetFiles(myConfig.FindDir, "*.exe");
        }

        /// <summary>
        /// 根据文件名查找和替换targDlls
        /// </summary>
        /// <param name="toDllInfos"></param>
        /// <param name="fromDllInfos"></param>
        private static void Replace(List<CopyFileInfo> toDllInfos, List<CopyFileInfo> fromDllInfos)
        {
            List<ReplaceFileInfo> replaceFileInfos = new List<ReplaceFileInfo>();
            foreach (var fromDll in fromDllInfos)
            {
                var toDll = toDllInfos.FirstOrDefault(x => x.FileName == fromDll.FileName);
                if (toDll == null)
                {
                    continue;
                }
                if (toDll.CreateTime < fromDll.CreateTime)
                {
                    ReplaceFileInfo replaceFileInfo = new ReplaceFileInfo()
                    {
                        newFile = fromDll,
                        targetFile = toDll
                    };
                    try
                    {
                        File.Copy(fromDll.FileFullName, toDll.FileFullName, true);
                        replaceFileInfo.ReplaceSuccess = true;
                        Log.Information($"succeed Copy {fromDll.FileFullName} to {toDll.FileFullName}");
                        if (_copyFileConfig.IsCopyPdb && fromDll.IsPdbExists)
                        {
                            File.Copy(Path.Combine(fromDll.FileDir, Path.GetFileNameWithoutExtension(fromDll.FileName) + ".pdb"),
                               Path.Combine(toDll.FileDir, Path.GetFileNameWithoutExtension(toDll.FileName) + ".pdb"), true);
                        }
                    }
                    catch (Exception)
                    {
                        replaceFileInfo.ReplaceSuccess = false;
                        Log.Information($"false Copy {fromDll.FileFullName} to {toDll.FileFullName}");
                    }
                    replaceFileInfos.Add(replaceFileInfo);
                }
            }
            Log.Warning($"replaceFileInfos count:{replaceFileInfos.Count}");
        }

        private static List<CopyFileInfo> GetInfosByFiles(string[] targDlls, bool IsFromToDir = true)
        {
            //实现这个方法
            var list = new List<CopyFileInfo>();
            foreach (var file in targDlls)
            {
                if (IsFromToDir && File.GetLastWriteTime(file) < _copyFileConfig.AfterTime)
                {
                    continue;
                }
                //如果list已经存在该文件，判断文件的创建时间,如果新文件的创建时间大于list中的文件，替换list中的文件
                var fName = Path.GetFileName(file);
                if (list.Any(x => x.FileName == fName))
                {
                    var info1 = list.FirstOrDefault(x => x.FileName == fName);
                    if (info1 != null && info1.CreateTime < File.GetCreationTime(file))
                    {
                        list.Remove(info1);
                    }
                    else
                    {
                        continue;
                    }
                }

                var info = new CopyFileInfo();
                info.FileFullName = file;
                info.FileName = Path.GetFileName(file);
                info.FileExt = Path.GetExtension(file);
                info.FileDir = Path.GetDirectoryName(file);
                info.CreateTime = File.GetLastWriteTime(file);
                info.IsPdbExists = File.Exists(Path.Combine(info.FileDir, Path.GetFileNameWithoutExtension(info.FileName) + ".pdb"));
                info.RelateDir = info.FileDir.Replace(IsFromToDir ? _copyFileConfig.FromDir : _copyFileConfig.ToDir, "");
                list.Add(info);
            }
            return list;
        }


    }
}
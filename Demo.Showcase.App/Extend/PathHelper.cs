using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Demo.Showcase.Extend
{
    public static class PathHelper
    {
        #region [PathHelper] - 动态查找指定模块 EXE
        /// <summary>
        /// 以 AppContext.BaseDirectory 为起点，向上找到 InterviewDemo 根目录，
        /// 再进入指定的子模块目录里递归搜 exeName，返回首个命中路径。
        /// </summary>
        public static string LocateExe(string moduleFolderName, string exeName)
        {
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);

            // 1️⃣ 向上查找 InterviewDemo
            while (dir is not null && !dir.Name.Equals("InterviewDemo", StringComparison.OrdinalIgnoreCase))
                dir = dir.Parent;

            if (dir is null)
                throw new InvalidOperationException("❌ InterviewDemo 根目录未找到。");

            // 2️⃣ 进入目标模块目录
            string moduleRoot = Path.Combine(dir.FullName, moduleFolderName);
            if (!Directory.Exists(moduleRoot))
                throw new InvalidOperationException($"❌ 未找到模块目录: {moduleRoot}");

            // 3️⃣ 递归搜索 exeName（兼容 Debug/Release/TFM）
            string? exePath = Directory.EnumerateFiles(moduleRoot, exeName, SearchOption.AllDirectories).FirstOrDefault();
            if (exePath is null)
                throw new InvalidOperationException($"❌ {exeName} 未找到。");

            return exePath;
        }
        #endregion
    }
}

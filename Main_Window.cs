﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Reflection;
using Microsoft.VisualBasic.Devices;
using System.Threading;
using System.Linq.Expressions;

namespace PvzLauncher
{
    public partial class Main_Window: AntdUI.Window
    {
        //函数========================================================================================
        #region Functions

        //引用==================================================
        //发送系统消息
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        //写PATH系统环境变量
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        UIntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out UIntPtr lpdwResult);

        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        //创建快捷方式
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxArgs);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig]
            int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }
        //引用==================================================

        //写日志
        public static void Log(string level, string message)
        {
            // 获取当前时间并格式化
            string timestamp = DateTime.Now.ToString("HH:mm:ss");

            // 构造完整日志条目
            string logContent = $"[{timestamp}][{level}]: {message}";

            // 拼接完整文件路径
            string logPath = Path.Combine(Application.StartupPath, "Log.log");

            // 使用追加模式写入文件
            using (StreamWriter sw = new StreamWriter(logPath, true))
            {
                sw.WriteLine(logContent);
            }

        }
        //连通性测试
        public static object CheckUrlConnection(string url)
        {
            // 验证URL格式有效性
            try
            {
                var uri = new Uri(url);
            }
            catch (UriFormatException)
            {
                return "unconnect";
            }

            HttpWebRequest request = null;
            Stopwatch sw = new Stopwatch();

            try
            {
                request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 5000;     // 设置5秒超时
                request.Method = "HEAD";     // 使用HEAD方法减少数据量

                sw.Start();
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    sw.Stop();
                    return sw.ElapsedMilliseconds;
                }
            }
            catch (WebException ex)
            {
                sw.Stop();
                /* 服务器响应但返回错误状态（如404）的情况
                   仍视为连接成功，返回延迟时间 */
                if (ex.Response != null)
                {
                    return sw.ElapsedMilliseconds;
                }
                return "unconnect"; // 真正无法连接的情况
            }
            catch (Exception)
            {
                return "unconnect";
            }
            finally
            {
                request?.Abort(); // 确保释放网络资源
            }
        }

        //执行控制台命令
        public string ExecuteCommand(string command)
        {
            try
            {
                var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    CreateNoWindow = false,          // 不创建新窗口
                    UseShellExecute = false,        // 不使用系统外壳程序执行
                    RedirectStandardError = true,   // 重定向标准错误
                    RedirectStandardOutput = true   // 重定向标准输出
                };

                using (var process = new Process())
                {
                    process.StartInfo = processInfo;
                    process.Start();

                    // 异步读取输出流和错误流
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();  // 等待程序执行完成

                    // 组合输出结果
                    string result = string.IsNullOrEmpty(output) ? "" : output;
                    string errorResult = string.IsNullOrEmpty(error) ? "" : "\n[Error]\n" + error;

                    return $"{result}{errorResult} (ExitCode: {process.ExitCode})";
                }
            }
            catch (Exception ex)
            {
                return $"执行命令时发生异常：{ex.Message}";
            }
        }

        //弹出系统通知
        public static void ShowNotification(string title, string content)
        {
            NotifyIcon notifyIcon = new NotifyIcon();

            // 创建透明图标
            using (Bitmap bmp = new Bitmap(1, 1))
            {
                bmp.SetPixel(0, 0, Color.Transparent);
                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    notifyIcon.Icon = Icon.FromHandle(hIcon);
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }

            notifyIcon.Visible = true;

            // 设置通知关闭后的清理操作
            notifyIcon.BalloonTipClosed += (sender, e) =>
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            };

            // 显示通知（3000ms=3秒显示时间）
            notifyIcon.ShowBalloonTip(3000, title, content, ToolTipIcon.None);
        }

        //写注册表项
        //rootKey常用常量
        //Registry.CurrentUser (HKEY_CURRENT_USER)
        //Registry.LocalMachine (HKEY_LOCAL_MACHINE)
        //Registry.ClassesRoot (HKEY_CLASSES_ROOT)

        /*valueKind：支持的类型包括：
        String：字符串值
        DWord：32位整数
        QWord：64位整数
        Binary：二进制数据
        MultiString：字符串数组*/
        public static bool WriteRegistryValue(RegistryKey rootKey, string subKeyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            try
            {
                if (rootKey == null)
                    throw new ArgumentNullException(nameof(rootKey));

                if (string.IsNullOrEmpty(subKeyPath))
                    throw new ArgumentException("子项路径不能为空", nameof(subKeyPath));

                using (RegistryKey subKey = rootKey.CreateSubKey(subKeyPath))
                {
                    if (subKey == null) return false;

                    subKey.SetValue(valueName, value, valueKind);
                    return true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 权限不足，可能需要以管理员身份运行
                throw;
            }
            catch (Exception ex)
            {
                // 记录异常或处理其他错误
                Console.WriteLine($"写入注册表失败: {ex.Message}");
                return false;
            }
        }

        //读注册表项
        public static object ReadRegistryValue(RegistryKey rootKey, string subKeyPath, string valueName, object defaultValue = null)
        {
            try
            {
                if (rootKey == null)
                    throw new ArgumentNullException(nameof(rootKey));

                if (string.IsNullOrEmpty(subKeyPath))
                    throw new ArgumentException("子项路径不能为空", nameof(subKeyPath));

                using (RegistryKey subKey = rootKey.OpenSubKey(subKeyPath, false))
                {
                    // 子项不存在时返回默认值
                    if (subKey == null) return defaultValue;

                    // 获取值（值不存在时返回默认值）
                    return subKey.GetValue(valueName, defaultValue);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 权限不足，可能需要管理员权限
                throw;
            }
            catch (Exception ex)
            {
                // 记录异常或处理其他错误
                Console.WriteLine($"读取注册表失败: {ex.Message}");
                return defaultValue;
            }
        }

        // 写入配置
        public static void WriteConfig(string filePath, string section, string key, string value)
        {
            var sections = ParseConfigFile(filePath);

            // 创建或更新节
            if (!sections.ContainsKey(section))
            {
                sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            // 更新键值
            sections[section][key.Trim()] = value;

            // 生成配置文件内容
            var lines = new List<string>();

            // 处理默认节（空节名）
            if (sections.TryGetValue("", out var defaultSection) && defaultSection.Count > 0)
            {
                lines.AddRange(defaultSection.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            }

            // 处理带节名的配置（按字母顺序排序）
            foreach (var sec in sections.Keys
                .Where(s => !string.IsNullOrEmpty(s))
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                // 添加节分隔空行
                if (lines.Count > 0) lines.Add("");

                lines.Add($"[{sec}]");
                lines.AddRange(sections[sec].Select(kvp => $"{kvp.Key}={kvp.Value}"));
            }

            File.WriteAllLines(filePath, lines, Encoding.UTF8);
        }

        // 读取配置
        public static string ReadConfig(string filePath, string section, string key)
        {
            if (!File.Exists(filePath)) return null;

            var sections = ParseConfigFile(filePath);

            if (sections.TryGetValue(section, out var sectionData) &&
                sectionData.TryGetValue(key, out var value))
            {
                return value;
            }
            return null;
        }

        // 解析配置文件为节字典(读写配置)
        private static Dictionary<string, Dictionary<string, string>> ParseConfigFile(string filePath)
        {
            var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            string currentSection = "";

            if (File.Exists(filePath))
            {
                foreach (var line in File.ReadAllLines(filePath))
                {
                    var trimmed = line.Trim();

                    // 处理节头
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                        if (!sections.ContainsKey(currentSection))
                        {
                            sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        }
                        continue;
                    }

                    // 处理键值对
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
                    {
                        var k = parts[0].Trim();
                        var v = parts[1].Trim();

                        if (!sections.ContainsKey(currentSection))
                        {
                            sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        }

                        sections[currentSection][k] = v;
                    }
                }
            }
            return sections;
        }

        //删除配置
        public static void DeleteConfig(string filePath, string section, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be empty", nameof(key));

            var sections = ParseConfigFile(filePath);
            section = section ?? "";

            if (sections.TryGetValue(section, out var sectionData) && sectionData.Remove(key))
            {
                // 如果节已空则移除整个节
                if (sectionData.Count == 0)
                {
                    sections.Remove(section);
                }

                SaveSectionsToFile(filePath, sections);

            }
        }

        // 统一保存配置的方法
        private static void SaveSectionsToFile(string filePath, Dictionary<string, Dictionary<string, string>> sections)
        {
            var lines = new List<string>();

            // 处理默认节
            if (sections.TryGetValue("", out var defaultSection) && defaultSection.Count > 0)
            {
                lines.AddRange(defaultSection.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            }

            // 处理带节名的配置（按字母排序）
            foreach (var sec in sections.Keys
                .Where(s => !string.IsNullOrEmpty(s))
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                var sectionData = sections[sec];
                if (sectionData.Count == 0) continue;

                if (lines.Count > 0) lines.Add("");
                lines.Add($"[{sec}]");
                lines.AddRange(sectionData.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            }

            File.WriteAllLines(filePath, lines, Encoding.UTF8);
        }

        //删除配置节
        public static void DeleteSection(string filePath, string section)
        {
            var sections = ParseConfigFile(filePath);
            section = section ?? "";

            if (sections.Remove(section))
            {
                SaveSectionsToFile(filePath, sections);
            }
        }

        //写PATH系统环境变量
        public static void AddPath(string directoryPath, bool systemLevel = true)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("目录路径不能为空");

            RegistryKey registryKey = systemLevel ?
                Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", true) :
                Registry.CurrentUser.OpenSubKey(@"Environment", true);

            if (registryKey == null)
                throw new NullReferenceException("注册表项未找到");

            try
            {
                string currentPath = registryKey.GetValue("PATH", "", RegistryValueOptions.DoNotExpandEnvironmentNames).ToString();
                string[] paths = currentPath.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                // 检查是否已存在（不区分大小写）
                if (paths.Any(p => p.Trim().Equals(directoryPath.Trim(), StringComparison.OrdinalIgnoreCase)))
                    return;

                // 追加新路径
                string newPath = currentPath.TrimEnd(';') + ";" + directoryPath.Trim();

                // 更新注册表
                registryKey.SetValue("PATH", newPath, RegistryValueKind.ExpandString);

                // 广播环境变量变更通知
                SendMessageTimeout(
                    new IntPtr(0xFFFF), // HWND_BROADCAST
                    WM_SETTINGCHANGE,
                    UIntPtr.Zero,
                    "Environment",
                    SMTO_ABORTIFHUNG,
                    5000,
                    out UIntPtr _);
            }
            finally
            {
                registryKey.Close();
            }
        }

        //创建快捷方式
        public static bool CreateShortcut(string targetPath, string shortcutPath)
        {
            if (!File.Exists(targetPath)) return false;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));

                var shellLink = (IShellLinkW)new ShellLink();
                shellLink.SetPath(targetPath);
                shellLink.SetWorkingDirectory(Path.GetDirectoryName(targetPath));
                shellLink.SetIconLocation(targetPath, 0);  // 使用目标文件自身图标

                var persistFile = (IPersistFile)shellLink;
                persistFile.Save(shortcutPath, false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        //程序自启动
        public static bool SetAutoStart(bool enable, string exePath = null, RegistryKey registryRoot = null, string keyName = null)
        {
            try
            {
                // 设置默认值
                exePath = exePath ?? Application.ExecutablePath;
                registryRoot = registryRoot ?? Registry.CurrentUser;
                keyName = keyName ?? Application.ProductName;

                // 获取注册表Run子项
                using (var runKey = registryRoot.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run",
                    true)) // 需要写权限
                {
                    if (runKey == null)
                    {
                        throw new Exception("无法打开注册表Run项");
                    }

                    if (enable)
                    {
                        // 设置自启动
                        runKey.SetValue(keyName, exePath);
                    }
                    else
                    {
                        // 移除自启动
                        runKey.DeleteValue(keyName, throwOnMissingValue: false);
                    }
                }
                return true;
            }
            catch (SecurityException ex)
            {
                MessageBox.Show($"需要管理员权限才能修改系统级自启动设置\n{ex.Message}");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"访问被拒绝，请以管理员身份运行\n{ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查当前自启动状态
        /// </summary>
        public static bool IsAutoStartEnabled(RegistryKey registryRoot = null,
                                            string keyName = null)
        {
            try
            {
                registryRoot = registryRoot ?? Registry.CurrentUser;
                keyName = keyName ?? Application.ProductName;

                using (var runKey = registryRoot.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    var value = runKey?.GetValue(keyName);
                    return value != null && value.ToString().Equals(
                        Application.ExecutablePath,
                        StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }

        //移动文件夹
        public bool MoveFolder(string source, string dest, bool overwrite, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                // 检查源文件夹
                if (!Directory.Exists(source))
                {
                    errorMessage = "源文件夹不存在";
                    return false;
                }

                // 判断是否跨磁盘移动（关键逻辑）
                bool isSameDrive = Path.GetPathRoot(source)?.ToUpper()
                                == Path.GetPathRoot(dest)?.ToUpper();

                // 处理目标文件夹已存在的情况
                if (Directory.Exists(dest))
                {
                    if (overwrite) Directory.Delete(dest, true);
                    else
                    {
                        errorMessage = "目标文件夹已存在且未启用覆盖";
                        return false;
                    }
                }

                // 执行移动
                if (isSameDrive)
                {
                    Directory.Move(source, dest); // 同一磁盘直接移动
                }
                else
                {
                    // 跨磁盘：复制+删除方案
                    new Computer().FileSystem.CopyDirectory(source, dest, overwrite);
                    Directory.Delete(source, true);
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"{(ex is IOException ? "IO错误" : ex.GetType().Name)}: {ex.Message}";
                return false;
            }
        }

        //复制文件夹
        public bool CopyFolder(string sourceFolder, string destFolder, bool overwrite)
        {
            try
            {
                // 检查源文件夹是否存在
                if (!Directory.Exists(sourceFolder))
                {
                    return false;
                }

                // 处理目标文件夹已存在的情况
                if (Directory.Exists(destFolder))
                {
                    if (overwrite)
                    {
                        // 递归删除目标文件夹
                        Directory.Delete(destFolder, true);
                    }
                    else
                    {
                        return false;
                    }
                }

                // 创建目标目录结构
                Directory.CreateDirectory(Path.GetDirectoryName(destFolder));

                // 执行复制操作（自动处理所有子内容和文件）
                new Computer().FileSystem.CopyDirectory(
                    sourceFolder,
                    destFolder,
                    overwrite
                );

                return true;
            }
            catch
            {
                return false;
            }
        }

        //异步HTTP下载文件
        public async Task DownloadFileAsync(string url, string savePath, IProgress<(int ProgressPercentage, long BytesReceived)> progress = null, CancellationToken cancellationToken = default)
        {
            using (var httpClient = new HttpClient())
            {
                // 获取响应头并验证状态
                using (var response = await httpClient.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    // 创建保存目录
                    var directory = Path.GetDirectoryName(savePath);
                    if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                    // 获取文件总大小
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var receivedBytes = 0L;

                    // 创建文件流
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(
                        savePath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 8192,
                        useAsync: true))
                    {
                        var buffer = new byte[8192];
                        int bytesRead;

                        // 分段下载并更新进度
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                            receivedBytes += bytesRead;

                            if (totalBytes > 0)
                            {
                                var progressPercentage = (int)((double)receivedBytes / totalBytes * 100);
                                progress?.Report((progressPercentage, receivedBytes));
                            }
                            else
                            {
                                progress?.Report((-1, receivedBytes));
                            }
                        }
                    }
                }
            }
        }

        //异步HTTP读文件
        public async Task<string> HttpReadFileAsync(string url)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // 设置超时时间（可选）
                    client.Timeout = TimeSpan.FromSeconds(30);

                    // 发送GET请求
                    using (HttpResponseMessage response = await client.GetAsync(url))
                    {
                        response.EnsureSuccessStatusCode();  // 确保响应成功
                        return await response.Content.ReadAsStringAsync();  // 读取内容为字符串
                    }
                }
            }
            catch (Exception ex)
            {
                // 这里可以记录异常或进行其他处理
                Console.WriteLine($"Error reading file: {ex.Message}");
                return null;  // 或者根据需求返回空字符串/抛出异常
            }
        }

        //异步HTTP下载文件带进度
        public async Task DownloadFileAsync(string url, string savePath, IProgress<int> progress)
        {
            using (WebClient webClient = new WebClient())
            {
                // 设置进度报告事件
                webClient.DownloadProgressChanged += (sender, e) =>
                {
                    progress?.Report(e.ProgressPercentage);
                };

                // 异步下载文件
                await webClient.DownloadFileTaskAsync(new Uri(url), savePath);
            }
        }
        #endregion

        //标题缓入
        public async void TitleFadeIn(int speed = 8)
        {
            if (ReadConfig(ConfigPath, "global", "TitleSkin") == "en")
            {
                pictureBox_Home_Title.Image = Properties.Resources.PvZ_Logo;
            }
            else
            {
                pictureBox_Home_Title.Image = Properties.Resources.PvZ_Logo_zh;
            }

            pictureBox_Home_Title.Top = 0 - pictureBox_Home_Title.Height;//标题移到窗口外上方

            for (int i = 0; i < pictureBox_Home_Title.Height / speed; i++)
            {
                pictureBox_Home_Title.Top = pictureBox_Home_Title.Top + speed;//传入速度
                await Task.Delay(1);//延迟
            }

            for (int i = 0; i < 2; i++)
            {
                pictureBox_Home_Title.Top = pictureBox_Home_Title.Top + 5;//下落缓冲
                await Task.Delay(1);
            }
            
            

            for (int i = 0; i < 10; i++)
            {
                pictureBox_Home_Title.Top = pictureBox_Home_Title.Top - 1;//回弹
                await Task.Delay(1);
            }


        }

        //加载游戏列表
        public async Task LoadGameList()
        {
            /*try
            {
                List<string> temp = new List<string>();//列表
                for (int i = 0; i < Directory.GetDirectories($"{RunPath}\\games").Length; i++)
                {
                    temp.Add(Path.GetFileName(Directory.GetDirectories($"{RunPath}\\games")[i]));//添加列表项
                }
                GamesPath = temp.ToArray();//转化数组
            }
            catch (Exception ex)
            {
                //错误报告
                AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                {
                    Title = "发生错误！",
                    Text = $"在加载游戏列表时发生错误！\n错误原因:{ex.Message}",
                    Icon = AntdUI.TType.Error
                });
                
            }*/


            try
            {
                List<string> temp = new List<string>();
                string gamesPath = Path.Combine(RunPath, "games");

                // 异步获取所有子目录
                string[] directories = await Task.Run(() => Directory.GetDirectories(gamesPath));

                // 遍历并添加文件名（非阻塞）
                foreach (string dir in directories)
                {
                    temp.Add(Path.GetFileName(dir));
                }

                GamesPath = temp.ToArray();
            }
            catch (Exception ex)
            {
                // 确保 UI 操作在主线程执行
                AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                {
                    Title = "发生错误！",
                    Text = $"在加载游戏列表时发生错误！\n错误原因:{ex.Message}",
                    Icon = AntdUI.TType.Error
                });
            }

        }

        //加载修改器列表
        public void LoadTrainerList()
        {
            try
            {
                List<string> temp = new List<string>();
                for (int i = 0; i < Directory.GetFiles($"{RunPath}\\trainer").Length; i++)
                {
                    temp.Add(Path.GetFileName(Directory.GetFiles($"{RunPath}\\trainer")[i]));
                }
                TrainerPath = temp.ToArray();


                select_Settings_Trainer_Select.Items.Clear();
                for (int i = 0; i < TrainerPath.Length; i++)
                {
                    select_Settings_Trainer_Select.Items.Add(TrainerPath[i]);
                }

                select_Settings_Trainer_Select.Text = ReadConfig(ConfigPath, "global", "SelectTrainer");
                STrainer = select_Settings_Trainer_Select.Text;


                try
                {
                    Icon trainericon = Icon.ExtractAssociatedIcon($"{RunPath}\\trainer\\{STrainer}");
                    image3D_Settings_Trainer.Image = trainericon.ToBitmap();
                }
                catch (Exception ex)
                {
                    /*AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                    {
                        Title = "发生错误！",
                        Text = $"在加载修改器图标时发生错误！\n\n错误原因:{ex.Message}",
                        Icon = AntdUI.TType.Error
                    });*/

                    string a = ex.Message;//使用ex变量，使其没有警告提示
                }

            }
            catch (Exception ex)
            {
                AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                {
                    Title = "发生错误！",
                    Text = $"在加载修改器信息时发生错误！\n\n错误原因:{ex.Message}",
                    Icon = AntdUI.TType.Error
                });

            }
        }

        //对齐控件
        public void Align()
        {
            #region 主页

            //标题居中
            pictureBox_Home_Title.Left = tabPage_Home.Width / 2 - pictureBox_Home_Title.Width / 2;

            //主标签栏大小
            tabs_Main.Width = this.Width;
            tabs_Main.Height = this.Height - 30;

            //控制栏宽度
            pageHeader.Width = this.Width;

            //按钮位置
            button_LaunchTrainer.Left = tabPage_Home.Width - button_LaunchTrainer.Width - 3;
            button_LaunchTrainer.Top = tabPage_Home.Height - button_LaunchTrainer.Height - 3;

            button_Launch.Left = tabPage_Home.Width - button_Launch.Width - 3;
            button_Launch.Top = tabPage_Home.Height - 3 - button_Launch.Height - button_LaunchTrainer.Height;

            button_GameSettings.Left = tabPage_Home.Width - button_GameSettings.Width - 3;
            button_GameSettings.Top = tabPage_Home.Height - 3 - button_LaunchTrainer.Height - button_Launch.Height - button_GameSettings.Height;

            button_SelectGame.Left = tabPage_Home.Width - button_Launch.Width - 3;
            button_SelectGame.Top = tabPage_Home.Height - 3 - button_LaunchTrainer.Height - button_Launch.Height - button_GameSettings.Height;

            //标签
            label_Home_Gamename.Left = tabPage_Home.Width - button_Launch.Width - 3;
            label_Home_Gamename.Top = tabPage_Home.Height - 3 - button_LaunchTrainer.Height - button_Launch.Height - button_GameSettings.Height - label_Home_Gamename.Height;

            //背景
            //pictureBox_Home_Background.Height = tabs_Main.Height - pictureBox_Home_Title.Height - 2;
            //pictureBox_Home_Background.Width = tabPage_Home.Width - button_Launch.Width - 5;
            pictureBox_Home_Background.Top = tabPage_Home.Height - 3 - pictureBox_Home_Background.Height;
            #endregion

            #region 设置

            //主标签栏
            tabs_Settings.Width = tabPage_Settings.Width;
            tabs_Settings.Height = tabPage_Settings.Height;

            //下载进度条
            progress_Settings_CheckUpdate.Width = tabPage_Launcher.Width - progress_Settings_CheckUpdate.Left - 5;
            #endregion

            #region 关于

            //背景图
            pictureBox_About_Background.Left = tabPage_About.Width - pictureBox_About_Background.Width;
            pictureBox_About_Background.Top = tabPage_About.Height - pictureBox_About_Background.Height;

            //信息
            label_About_info4.Top = tabPage_About.Height - label_About_info4.Height - 2;
            label_About_info3.Top = tabPage_About.Height - 2 - label_About_info4.Height - 2 - label_About_info3.Height;
            pictureBox_About_Egg.Top = tabPage_About.Height - 2 - label_About_info4.Height - 2 - label_About_info3.Height - 2 - pictureBox_About_Egg.Height;

            #endregion
        }

        //检查更新
        public async void CheckUpdate(bool LaunchTime = false)
        {
            button_CheckUpdate.Loading = true;
            
            string Updateinfo = await HttpReadFileAsync("https://gitee.com/huamouren110/pvz-launcher/raw/master/update/version");

            if (Updateinfo == "")
            {
                AntdUI.Modal.open(new AntdUI.Modal.Config(this, "", "")
                {
                    Title = "检测失败！",
                    Content = "无法连接到更新服务器: https://gitee.com/huamouren110/pvz-launcher/raw/master/update/version \n\n请检查你的网络连接！",
                    CancelText = null,
                    OkText = "确定",
                    Icon = AntdUI.TType.Error
                });
            }
            else if (Updateinfo == Version)
            {
                if (LaunchTime != true)
                {
                    AntdUI.Modal.open(new AntdUI.Modal.Config(this, "", "")
                    {
                        Title = "无可用更新",
                        Content = "您使用的是最新版本",
                        CancelText = null,
                        OkText = "确定",
                        Icon = AntdUI.TType.Success
                    });
                }

            }
            else if (Updateinfo != Version)
            {
                if (AntdUI.Modal.open(new AntdUI.Modal.Config(this, "", "")
                {
                    Title = "检测到更新！",
                    Content = $"检测到新版本！\n\n最新版本:{Updateinfo}\n当前版本:{Version}\n\n是否下载最新版本？",
                    CancelText = "取消",
                    OkText = "下载更新",
                    Icon = AntdUI.TType.Warn
                }) == DialogResult.OK)
                {
                    progress_Settings_CheckUpdate.Visible = true;

                    var progress = new Progress<int>(percent =>
                    {
                        progress_Settings_CheckUpdate.Value = percent / 100F;
                    });

                    try
                    {
                        await DownloadFileAsync("https://gitee.com/huamouren110/pvz-launcher/raw/master/update/PvzLauncher_.exe", $"{RunPath}\\PvzLauncher_.exe", progress);

                        Process.Start($"{RunPath}\\UpdateService.exe");
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                        {
                            Title = "下载失败！",
                            Text = $"在下载更新文件时发生错误！\n\n错误原因:{ex.Message}",
                            Icon = AntdUI.TType.Error
                        });
                        progress_Settings_CheckUpdate.Visible = false;
                    }

                }
            }
            button_CheckUpdate.Loading = false;

        }

        //对象========================================================================================
        Random random = new Random();    //随机数生成器
        SelectGame_Window selectGame_Window = new SelectGame_Window();    //选择游戏窗口
        SetGame_Window setGame_Window = new SetGame_Window();    //设置游戏窗口
        Process proceess = new Process();    //进程管理
        //变量========================================================================================
        public static string Title = "Plants vs. Zombies Launcher";    //窗口标题
        public static string Version = "Pre-Release 1.1.1.8";    //版本
        public static string CompliedTime = "2025-5-24 16:46";     //编译时间
        public static string RunPath = Directory.GetCurrentDirectory();     //运行目录
        public static string ConfigPath = $"{RunPath}\\config\\config.ini";    //配置文件目录
        public static string[] GamesPath;    //游戏列表
        public static string SGamesPath;    //当前游戏路径
        public static string[] TrainerPath;    //修改器列表
        public static string STrainer;    //当前修改器路径
        public static int EggNum = 0;    //彩蛋计数器
        public static string[] args = Environment.GetCommandLineArgs();    //启动参数
        //事件========================================================================================
        //构造函数
        public Main_Window()
        {
            InitializeComponent();//初始化

            //初始化================================

            pageHeader.Text = $"{Title}";//设置标题

            //加载修改器信息
            LoadTrainerList();

            label_About_info3.Text = $"版本:{Version}  编译时间:{CompliedTime}";//设置关于界面版本信息

            //tabs初始选项卡
            tabs_Main.SelectedIndex = 0;
            tabs_Settings.SelectedIndex = 0;

            //初始化end================================


            //初始化配置文件
            try
            {
                //初始化配置文件
                if (!Directory.Exists($"{RunPath}\\config"))
                {
                    Directory.CreateDirectory($"{RunPath}\\config");
                }
                //生成默认配置
                if (!File.Exists(ConfigPath))
                {
                    //global
                    WriteConfig(ConfigPath, "global", "SelectGame", "请选择游戏");//当前选择的游戏
                    WriteConfig(ConfigPath, "global", "TrainerWithGameLaunch", "false");//启动器随游戏启动
                    WriteConfig(ConfigPath, "global", "LaunchedExecute", "0");//游戏启动后的操作
                    WriteConfig(ConfigPath, "global", "SelectTrainer", "PvzToolkit_1.22.0.exe");//当前选择的修改器
                    WriteConfig(ConfigPath, "global", "WindowWidth", $"{this.Width}");//宽度
                    WriteConfig(ConfigPath, "global", "WindowHeight", $"{this.Height}");//高度
                    WriteConfig(ConfigPath, "global", "TitleSkin", "en");//标题皮肤
                    WriteConfig(ConfigPath, "global", "LaunchCheckUpdate", "true");//启动时检查更新
                    WriteConfig(ConfigPath, "global", "FirstLaunch", "true");//首次启动

                    //games
                    WriteConfig(ConfigPath, "Plants Vs Zombies", "ExecuteName", "PlantsVsZombies.exe");//默认游戏的名称
                    WriteConfig(ConfigPath, "Plants Vs Zombies", "PlayTime", "0");
                }
            }
            catch (Exception ex)
            {
                //错误报告
                AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                {
                    Title = "发生错误！",
                    Text = $"在初始化配置文件时发生错误！\n错误原因:{ex.Message}",
                    Icon = AntdUI.TType.Error
                });

            }

            //读配置项
            try
            {
                //当前选择的游戏
                SGamesPath = ReadConfig(ConfigPath, "global", "SelectGame");

                //修改器随游戏启动
                if (ReadConfig(ConfigPath, "global", "TrainerWithGameLaunch") == "true")
                {
                    switch_Settings_TrainerWithGame.Checked = true;
                }
                else if (ReadConfig(ConfigPath, "global", "TrainerWithGameLaunch") == "false")
                {
                    switch_Settings_TrainerWithGame.Checked = false;
                }
                else
                {
                    WriteConfig(ConfigPath, "global", "TrainerWithGameLaunch", "false");//恢复默认值
                    AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                    {
                        Title = "发生错误！",
                        Text = "在读取配置项 config.ini -> global -> TrainerWithGameLaunch 时发生错误！\n\n错误原因: 其配置项的值只能为 true 或 false 而该配置项为其他值！",
                        Icon = AntdUI.TType.Error
                    });
                }

                //游戏启动后的操作
                select_Launcher_Ld.SelectedIndex = int.Parse(ReadConfig(ConfigPath, "global", "LaunchedExecute"));

                //大小
                this.Width = int.Parse(ReadConfig(ConfigPath, "global", "WindowWidth"));
                this.Height = int.Parse(ReadConfig(ConfigPath, "global", "WindowHeight"));

                //标题皮肤
                if (ReadConfig(ConfigPath, "global", "TitleSkin") == "en")
                {
                    pictureBox_Home_Title.Image = Properties.Resources.PvZ_Logo;
                    radio_Settings_Launcher_Skin1.Checked = true;
                    radio_Settings_Launcher_Skin2.Checked = false;
                }
                else
                {
                    pictureBox_Home_Title.Image = Properties.Resources.PvZ_Logo_zh;
                    radio_Settings_Launcher_Skin1.Checked = false;
                    radio_Settings_Launcher_Skin2.Checked = true;
                }

                //启动时检查更新
                if (ReadConfig(ConfigPath, "global", "LaunchCheckUpdate") == "true")
                {
                    switch_Settings_Launcher_LaunchCheckUpdate.Checked = true;
                }
                else
                {
                    switch_Settings_Launcher_LaunchCheckUpdate.Checked = false;
                }

            }
            catch (Exception ex)
            {
                AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                {
                    Title = "发生错误！",
                    Text = $"在应用配置项时发生错误！\n错误原因:{ex.Message}",
                    Icon = AntdUI.TType.Error
                });

            }






            //初始化游戏文件夹
            if (!Directory.Exists($"{RunPath}\\games"))
            {
                Directory.CreateDirectory($"{RunPath}\\games");
                AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                {
                    Title = "提示",
                    Text = "游戏关键性文件夹 games 不存在，已成功创建！",
                    Icon = AntdUI.TType.Warn
                });
            }
            if (!Directory.Exists($"{RunPath}\\trainer"))
            {
                Directory.CreateDirectory($"{RunPath}\\trainer");
                AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                {
                    Title = "提示",
                    Text = "游戏关键性文件夹 trainer 不存在，已成功创建！",
                    Icon = AntdUI.TType.Warn
                });
            }


            //游戏资源检测
            if (!File.Exists($"{RunPath}\\assets\\user1.dat") | !File.Exists($"{RunPath}\\UpdateService.exe") | !File.Exists($"{RunPath}\\assets\\bass.dll") | !File.Exists($"{RunPath}\\assets\\gdi42.dll")) 
            {
                AntdUI.Modal.open(new AntdUI.Modal.Config(this, "", "")
                {
                    Title = "启动器文件缺失！",
                    Content = "启动器主要文件缺失！请完整下载软件发布时携带的附属文件！",
                    OkText = "确定",
                    CancelText = null,
                    Icon = AntdUI.TType.Error
                });
                this.Close();
            }





            





            
        }

        //窗口加载
        private async void Main_Window_Load(object sender, EventArgs e)
        {

            await LoadGameList();//加载游戏列表

            if (ReadConfig(ConfigPath, "global", "LaunchCheckUpdate") == "true")
            {
                CheckUpdate(true);
            }
            

            //游戏&修改器检测

            if (GamesPath.Length == 0)
            {
                AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                {
                    Title = "提示",
                    Text = "你还没有添加任何游戏，请在选择版本界面添加游戏！",
                    Icon = AntdUI.TType.Warn
                });
            }
            if (TrainerPath.Length == 0)
            {
                AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                {
                    Title = "提示",
                    Text = "你没有添加任何修改器！请在设置界面添加修改器！",
                    Icon = AntdUI.TType.Error
                });
            }

            

            //更新检测
            if (args.Length == 2)
            {
                if (args[1] == "-update")
                {
                    AntdUI.Modal.open(new AntdUI.Modal.Config(this, "", "")
                    {
                        Title = $"启动器已更新至{Version}",
                        Content = "开始使用吧！",
                        Icon = AntdUI.TType.Success,
                        OkText = "确定",
                        CancelText = null
                    });
                }
            }

            if (ReadConfig(ConfigPath, "global", "FirstLaunch") == "true")
            {
                AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                {
                    Title = "欢迎！",
                    Text = "欢迎使用PvzLauncher！\n\n点击\"选择版本\"按钮开始游戏吧！",
                    Icon = AntdUI.TType.Success
                });
                WriteConfig(ConfigPath, "global", "FirstLaunch", "false");
            }


        }

        //窗口大小改变
        private void Main_Window_Resize(object sender, EventArgs e)
        {
            Align();
        }

        //窗口大小改变结束
        private void Main_Window_ResizeEnd(object sender, EventArgs e)
        {
            WriteConfig(ConfigPath, "global", "WindowWidth", $"{this.Width}");
            WriteConfig(ConfigPath, "global", "WindowHeight", $"{this.Height}");
        }

        //主标签栏索引改变
        private void tabs_Main_SelectedIndexChanged(object sender, AntdUI.IntEventArgs e)
        {

            if (tabs_Main.SelectedIndex == 0)
            {
                TitleFadeIn();
            }

        }

        //选择游戏
        private void button_SelectGame_Click(object sender, EventArgs e)
        {
            

            selectGame_Window.ShowDialog();//选择游戏
        }

        //时间主循环
        private void timer_Main_Tick(object sender, EventArgs e)
        {
            //Align();
            label_Home_Gamename.Text = $"{SGamesPath}";//设置当前游戏名称
        }

        //启动游戏
        private async void button_Launch_Click(object sender, EventArgs e)
        {
            #region 启动/结束游戏
            
            //设置process信息
            proceess.StartInfo.FileName = $"{RunPath}\\games\\{SGamesPath}\\{ReadConfig(ConfigPath, $"{SGamesPath}", "ExecuteName")}";

            if (button_Launch.Text == "启动游戏") 
            {
                try
                {
                    //按钮形态改变
                    button_Launch.Text = "结束进程";
                    button_Launch.Type = AntdUI.TTypeMini.Error;
                    button_Launch.Icon = Properties.Resources.close;
                    timer_PlayTime.Enabled = true;

                    button_GameSettings.Enabled = false;
                    button_SelectGame.Enabled = false;

                    //吊起进程
                    proceess.Start();

                    //启动器是否应该启动
                    if (ReadConfig(ConfigPath, "global", "TrainerWithGameLaunch") == "true")
                    {
                        button_LaunchTrainer_Click(sender, e);//启动修改器
                    }

                    //执行什么操作
                    if (ReadConfig(ConfigPath, "global", "LaunchedExecute") == "1")
                    {
                        this.Close();
                    }
                    if (ReadConfig(ConfigPath, "global", "LaunchedExecute") == "2")
                    {
                        this.Visible = false;
                    }




                    //成功提示
                    AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                    {
                        Title = "成功启动！",
                        Text = $"游戏{SGamesPath}成功启动！",
                        Icon = AntdUI.TType.Success
                    });

                    if (ReadConfig(ConfigPath, SGamesPath, "FirstLaunch") == null)
                    {
                        WriteConfig(ConfigPath, SGamesPath, "FirstLaunch", $"{DateTime.Now.Year}年{DateTime.Now.Month}月{DateTime.Now.Day}日 {DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}");
                    }




                    

                    //等待进程退出
                    await Task.Run(() => proceess.WaitForExit());//第1000行代码！！！
                    //按钮形态改变
                    button_Launch.Text = "启动游戏";
                    button_Launch.Type = AntdUI.TTypeMini.Success;
                    button_Launch.Icon = Properties.Resources.launch;
                    timer_PlayTime.Enabled = false;

                    button_GameSettings.Enabled = true;
                    button_SelectGame.Enabled = true;

                    //执行操作
                    if (ReadConfig(ConfigPath, "global", "LaunchedExecute") == "2")
                    {
                        this.Visible = true;
                    }





                    //进程退出提示
                    AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                    {
                        Title = "进程已退出",
                        Text = $"游戏{SGamesPath}已退出!",
                        Icon = AntdUI.TType.Info
                    });
                }
                catch (Exception ex)
                {
                    //错误报告
                    AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                    {
                        Title = "发生错误！",
                        Text = $"在启动游戏进程时发生错误！\n错误原因:{ex.Message}",
                        Icon = AntdUI.TType.Error
                    });

                    //按钮形态改变
                    button_Launch.Text = "启动游戏";
                    button_Launch.Type = AntdUI.TTypeMini.Success;
                    button_Launch.Icon = Properties.Resources.launch;
                    timer_PlayTime.Enabled = false;

                    button_GameSettings.Enabled = true;
                    button_SelectGame.Enabled = true;

                    //进程退出提示
                    AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                    {
                        Title = "进程已退出",
                        Text = $"游戏{SGamesPath}已退出!",
                        Icon = AntdUI.TType.Info
                    });
                }
                
            }
            else
            {
                //当前进程为运行状态
                if (!proceess.HasExited)
                {
                    try
                    {
                        //按钮状态改变
                        button_Launch.Text = "启动游戏";
                        button_Launch.Type = AntdUI.TTypeMini.Success;
                        button_Launch.Icon = Properties.Resources.launch;
                        timer_PlayTime.Enabled = false;

                        button_GameSettings.Enabled = true;
                        button_SelectGame.Enabled = true;

                        //结束进程
                        proceess.Kill();
                    }
                    catch (Exception ex)
                    {
                        //错误报告
                        AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                        {
                            Title = "发生错误！",
                            Text = $"在结束游戏进程时发生错误！\n错误原因:{ex.Message}",
                            Icon = AntdUI.TType.Error
                        });
                       
                    }
                }
                
                
            }

            #endregion
        }

        //彩蛋
        private void pictureBox1_Click(object sender, EventArgs e)
        {
            #region 彩蛋
            EggNum = EggNum + 1;
            if (EggNum == 10)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "你是听说这里有一个彩蛋才来点的对吧",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 15)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "听着，这里没有彩蛋，彩蛋在其他地方",
                    Icon = AntdUI.TType.Warn,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 20)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "嗯..看起来你真的很闲",
                    Icon = AntdUI.TType.Warn,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 25)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "你可以去干一些其他的事情。打打小游戏，找找改版...\n而不是在这里点击图片！",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 30)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "你还在点？",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 35)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "恭喜！35次点击",
                    Icon = AntdUI.TType.Success,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 40)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "好吧实话告诉你，彩蛋确实是这个，但是只是这些弹窗而已。",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 45)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "好了，到此为止，彩蛋做这么多就没了，后面就没了",
                    Icon = AntdUI.TType.Warn,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 50)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "好了，不要再点了",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 55)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "这个图标其实是一个设计时随手添加的一个东西",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 60)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "在最初设计的时候，这里缺了一些东西，于是就是用了PVZ标题来填充",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 65)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "之后或许会被删掉。你就没有东西可以玩了！",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 70)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "好了，我该告诉你的都告诉你了。你可以走了，彩蛋就做了这么点",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 75)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "不要再点了",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 80)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "哈哈，这样你就点不了了",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
                tabs_Main.SelectedIndex = 0;

            }
            else if (EggNum == 85)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "你怎么又切换回来了？",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 90)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "好了！到此为止！！！",
                    Icon = AntdUI.TType.Error,
                    ShowInWindow = true
                });
                

            }
            else if (EggNum == 100)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "不 要 再 点 了！！！！",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 105)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "看来我必须做出一些措施了",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 110)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "哈哈，你的控制按钮被我吃掉了！你关不掉这个程序了！",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
                pageHeader.ShowButton = false;
            }
            else if (EggNum == 115)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试长文本测试\n",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 120)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "我想你的电脑刚刚肯定快炸了吧",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 125)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "...",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 130)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "我可没有那么多时间陪你玩了，我还要改BUG，添加新功能...",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 135)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "好吧，只能使用那一招了",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 140)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "再见！",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
            }
            else if (EggNum == 145)
            {
                AntdUI.Message.open(new AntdUI.Message.Config(this, "", AntdUI.TType.None)
                {
                    Text = "",
                    Icon = AntdUI.TType.Info,
                    ShowInWindow = true
                });
                this.Close();
            }



            else if (1 > 2 && 2 < 1)
            {
                //你一定是翻看代码才看到这段话的对吧，正常操作根本无法到达此分支。即使你使用了CE修改器
            }

            #endregion
        }

        //启动修改器
        public void button_LaunchTrainer_Click(object sender, EventArgs e)
        {
            //启动修改器
            try
            {
                Process.Start($"{RunPath}\\trainer\\{STrainer}");

                AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                {
                    Title = "启动成功！",
                    Text = "修改器已成功启动！",
                    Icon = AntdUI.TType.Success
                });

            }
            catch (Exception ex)
            {

                AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                {
                    Title = "发生错误！",
                    Text = $"在启动修改器进程时发生错误！\n错误原因:{ex.Message}",
                    Icon = AntdUI.TType.Error
                });
            }
        }

        //游戏设置
        private void button_GameSettings_Click(object sender, EventArgs e)
        {
            //调用版本设置界面
            setGame_Window.ShowDialog();
        }

        //关于->github
        private void button_About_Github_Click(object sender, EventArgs e)
        {
            //跳转github
            Process.Start("https://www.github.com/bilibilihuazi/PvzLauncher");
        }

        //关于->bilibili
        private void button_About_Bilibili_Click(object sender, EventArgs e)
        {
            //跳转bilibili
            Process.Start("https://space.bilibili.com/1794899926");
        }

        //设置->删除存档
        private void button_Settings_RemoveSave_Click(object sender, EventArgs e)
        {
            //删除存档
            if (AntdUI.Modal.open(new AntdUI.Modal.Config(this, "", "")
            {
                Title = "确认操作",
                Content = "此操作会移除游戏的所有存档，是否删除？",
                CancelText = "我再想想",
                OkText = "删除存档",
                OkType = AntdUI.TTypeMini.Error,
                Icon = AntdUI.TType.Warn
            }) == DialogResult.OK) 
            {
                if(AntdUI.Modal.open(new AntdUI.Modal.Config(this, "", "")
                {
                    Title = "最后一次确认",
                    Content = "此对话框为最后一次警告，确认后立即删除存档！请慎重考虑！",
                    CancelText = "我不删除了",
                    OkText = "继续删除",
                    OkType = AntdUI.TTypeMini.Error,
                    Icon = AntdUI.TType.Error
                }) == DialogResult.OK)
                {
                    if (button_Launch.Text != "结束进程")//判断游戏是否运行中
                    {
                        try
                        {
                            DirectoryInfo directoryInfo = new DirectoryInfo($"C:\\ProgramData\\PopCap Games\\PlantsVsZombies\\userdata");

                            directoryInfo.Delete(true);


                            AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                            {
                                Title = "成功删除！",
                                Text = "存档删除成功！",
                                Icon = AntdUI.TType.Success
                            });
                        }
                        catch (Exception ex)
                        {

                            AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                            {
                                Title = "发生错误！",
                                Text = $"在删除存档文件夹时发生错误！\n错误原因:{ex.Message}",
                                Icon = AntdUI.TType.Error
                            });
                        }
                    }
                    else
                    {
                        AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                        {
                            Title = "发生错误！",
                            Text = "检测到游戏正在运行，请先结束游戏进程！",
                            Icon = AntdUI.TType.Error
                        });
                    }

                    
                }
            }
        }

        //设置->通关存档
        private void button_Settings_TotalSave_Click(object sender, EventArgs e)
        {
            if(AntdUI.Modal.open(new AntdUI.Modal.Config(this, "", "")
            {
                Title = "是否替换？",
                Content = "此操作会替换首个用户的存档文件(第一个创建的用户)；是否继续?",
                CancelText = "取消替换",
                OkText = "开始替换",
                Icon = AntdUI.TType.Warn
            }) == DialogResult.OK)
            {
                if(!File.Exists($"C:\\ProgramData\\PopCap Games\\PlantsVsZombies\\userdata\\user1.dat"))
                {
                    AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                    {
                        Title = "发生错误！",
                        Text = "存档文件不存在，请先创建一个用户！",
                        Icon = AntdUI.TType.Error
                    });
                }
                else
                {
                    try
                    {
                        if(File.Exists($"C:\\ProgramData\\PopCap Games\\PlantsVsZombies\\userdata\\user1.dat"))//存档文件夹
                        {
                            File.Delete($"C:\\ProgramData\\PopCap Games\\PlantsVsZombies\\userdata\\user1.dat");

                        }
                         
                        File.Copy($"{RunPath}\\config\\user1.dat", $"C:\\ProgramData\\PopCap Games\\PlantsVsZombies\\userdata\\user1.dat");

                        AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                        {
                            Title = "替换成功！",
                            Text = "通关存档已成功替换，请在游戏中重新加载存档\n(更换用户->确定)",
                            Icon = AntdUI.TType.Success
                        });
                    }
                    catch (Exception ex)
                    {
                        AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                        {
                            Title = "发生错误！",
                            Text = $"替换存档失败！\n错误原因:{ex.Message}",
                            Icon = AntdUI.TType.Error
                        });
                        
                    }
                }
            }
        }

        //设置->修改器随游戏启动
        private void switch_Settings_TrainerWithGame_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (switch_Settings_TrainerWithGame.Checked == true)
            {
                WriteConfig(ConfigPath, "global", "TrainerWithGameLaunch", "true");
            }
            else
            {
                WriteConfig(ConfigPath, "global", "TrainerWithGameLaunch", "false");
            }
        }

        //游戏时间计时器
        private void timer_PlayTime_Tick(object sender, EventArgs e)
        {
            int temp = int.Parse(ReadConfig(ConfigPath, SGamesPath, "PlayTime"));
            WriteConfig(ConfigPath, SGamesPath, "PlayTime", $"{temp + 1}");
        }

        //设置->启动器启动后操作
        private void select_Launcher_Ld_SelectedIndexChanged(object sender, AntdUI.IntEventArgs e)
        {
            WriteConfig(ConfigPath, "global", "LaunchedExecute", $"{select_Launcher_Ld.SelectedIndex}");
        }

        //设置主标签栏索引改变
        private void tabs_Settings_SelectedIndexChanged(object sender, AntdUI.IntEventArgs e)
        {
            if (tabs_Settings.SelectedIndex == 2)
            {
                LoadTrainerList();
                
                


            }
        }

        //设置->选择修改器
        private void select_Settings_Trainer_Select_SelectedIndexChanged(object sender, AntdUI.IntEventArgs e)
        {
            WriteConfig(ConfigPath, "global", "SelectTrainer", select_Settings_Trainer_Select.Text);
            STrainer = select_Settings_Trainer_Select.Text;

            try
            {
                Icon trainericon = Icon.ExtractAssociatedIcon($"{RunPath}\\trainer\\{STrainer}");
                image3D_Settings_Trainer.Image = trainericon.ToBitmap();
            }
            catch (Exception ex)
            {
                AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                {
                    Title = "发生错误！",
                    Text = $"在加载修改器图标时发生错误！\n\n错误原因:{ex.Message}",
                    Icon = AntdUI.TType.Error
                });
            }
        }

        //设置->导入修改器
        private void button_Settings_Trainer_Load_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                if (AntdUI.Modal.open(new AntdUI.Modal.Config(this, "", "")
                {
                    Title = "提示",
                    Content = $"是否将 {openFileDialog.FileName} 导入进启动器目录内？\n\n源文件不受影响",
                    CancelText = "取消",
                    OkText = "导入",
                    Icon = AntdUI.TType.Warn
                }) == DialogResult.OK) 
                {
                    try
                    {
                        File.Copy(openFileDialog.FileName, $"{RunPath}\\trainer\\{Path.GetFileName(openFileDialog.FileName)}");

                        LoadTrainerList();


                        AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                        {
                            Title = "导入成功！",
                            Text = "修改器已导入启动器目录中",
                            Icon = AntdUI.TType.Success
                        });
                    }
                    catch (Exception ex)
                    {
                        AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                        {
                            Title = "发生错误！",
                            Text = $"在导入修改器时发生错误！\n\n错误原因:{ex.Message}",
                            Icon = AntdUI.TType.Error
                        });
                        
                    }
                }
            }
        }

        //设置->删除修改器
        private void button_Settings_Trainer_Delete_Click(object sender, EventArgs e)
        {
            if(AntdUI.Modal.open(new AntdUI.Modal.Config(this, "", "")
            {
                Title = "提示",
                Content = "是否删除当前选择的修改器？",
                CancelText = "取消",
                OkText = "删除",
                OkType = AntdUI.TTypeMini.Error,
                Icon = AntdUI.TType.Warn
            }) == DialogResult.OK)
            {
                try
                {
                    File.Delete($"{RunPath}\\trainer\\{STrainer}");

                    LoadTrainerList();

                    WriteConfig(ConfigPath, "global", "SelectTrainer", "");

                    AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                    {
                        Title = "删除成功！",
                        Text = "选中的修改器已删除！",
                        Icon = AntdUI.TType.Success
                    });
                }
                catch (Exception ex)
                {
                    AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                    {
                        Title = "发生错误！",
                        Text = $"在删除修改器文件时发生错误！\n\n错误原因:{ex.Message}",
                        Icon = AntdUI.TType.Error
                    });
                    
                }
            }
        }

        //设置->打开存档文件夹
        private void button_Settings_OpenSave_Click(object sender, EventArgs e)
        {
            if(Directory.Exists("C:\\ProgramData\\PopCap Games\\PlantsVsZombies\\userdata"))
            {
                Process.Start($"C:\\ProgramData\\PopCap Games\\PlantsVsZombies\\userdata");

            }
            else
            {
                AntdUI.Notification.open(new AntdUI.Notification.Config(this, "", "", AntdUI.TType.None, AntdUI.TAlignFrom.TR)
                {
                    Title = "发生错误！",
                    Text = "存档文件夹不存在，请进入游戏创建一个存档",
                    Icon = AntdUI.TType.Error
                });
            }
        }

        //主页tabpage绘制
        private void tabPage_Home_Paint(object sender, PaintEventArgs e)
        {
            Align();
        }

        //设置tabpage绘制
        private void tabPage_Settings_Paint(object sender, PaintEventArgs e)
        {
            Align();
        }

        //关于tabpage绘制
        private void tabPage_About_Paint(object sender, PaintEventArgs e)
        {
            Align();
        }

        //设置->标题皮肤en
        private void radio_Settings_Launcher_Skin1_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (radio_Settings_Launcher_Skin1.Checked == true)
            {
                WriteConfig(ConfigPath, "global", "TitleSkin", "en");
            }
            else
            {
                WriteConfig(ConfigPath, "global", "TitleSkin", "zh");
            }
        }

        //设置->标题皮肤zh
        private void radio_Settings_Launcher_Skin2_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (radio_Settings_Launcher_Skin2.Checked == true)
            {
                WriteConfig(ConfigPath, "global", "TitleSkin", "zh");
            }
            else
            {
                WriteConfig(ConfigPath, "global", "TitleSkin", "en");
            }
        }

        //关于->检测更新
        private void button_CheckUpdate_Click(object sender, EventArgs e)
        {
            button_CheckUpdate.Loading = true;
            button_CheckUpdate.Text = "检测中...";

            CheckUpdate();

            button_CheckUpdate.Loading = false;
            button_CheckUpdate.Text = "检测更新";
        }

        //设置->启动器启动时检查更新
        private void switch_Settings_Launcher_LaunchCheckUpdate_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (switch_Settings_Launcher_LaunchCheckUpdate.Checked == true)
            {
                WriteConfig(ConfigPath, "global", "LaunchCheckUpdate", "true");
            }
            else
            {
                WriteConfig(ConfigPath, "global", "LaunchCheckUpdate", "false");
            }
        }

        //关于->Gitee
        private void button_About_Gitee_Click(object sender, EventArgs e)
        {
            Process.Start("https://gitee.com/huamouren110/pvz-launcher");
        }

        //关于->QQ
        private void button_About_QQ_Click(object sender, EventArgs e)
        {
            if(AntdUI.Modal.open(new AntdUI.Modal.Config(this, "", "")
            {
                Title = "提示",
                Content = "QQ群：1040764053\n\n是否复制到剪贴板？",
                OkText = "确定",
                CancelText = "取消",
                Icon = AntdUI.TType.Warn
            }) == DialogResult.OK)
            {
                Clipboard.SetText("1040764053");
            }
            
        }
    }
}
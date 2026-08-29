using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("单位内网登陆工具")]
[assembly: AssemblyDescription("将 115.24.187.161 加入系统代理例外列表,并用 Chrome 打开单位登陆页")]
[assembly: AssemblyCompany("LHStudio")]
[assembly: AssemblyProduct("Unit Login Launcher")]
[assembly: AssemblyCopyright("Copyright © LHStudio 2026")]
[assembly: AssemblyTrademark("LHStudio")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0.0")]

namespace UnitLogin
{
    internal static class Program
    {
        private const string HostAddress = "115.24.187.161";
        private const string Url = "http://115.24.187.161/";

        [STAThread]
        private static int Main(string[] args)
        {
            bool remove = false;
            bool about = false;
            foreach (string arg in args)
            {
                string flag = arg.Trim().TrimStart('-', '/').ToLowerInvariant();
                if (flag == "remove") remove = true;
                if (flag == "about") about = true;
            }

            if (about)
            {
                ShowAbout();
                return 0;
            }

            try
            {
                UpdateProxyBypass(remove);

                if (remove)
                {
                    MessageBox.Show("Removed " + HostAddress + " from the proxy bypass list.",
                        "Unit login launcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                LaunchChrome();
                return 0;
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                if (string.IsNullOrEmpty(message)) message = ex.GetType().Name;
                MessageBox.Show(message, "Unit login launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static void ShowAbout()
        {
            string text =
                "单位内网登陆工具 (Unit Login Launcher)\n" +
                "版本 1.0.0.0    作者: LHStudio\n" +
                "\n" +
                "功能:\n" +
                "  将 115.24.187.161 加入系统代理例外列表,\n" +
                "  并用 Chrome 新窗口打开 http://115.24.187.161/\n" +
                "\n" +
                "命令行参数:\n" +
                "  (无参数)     添加代理例外并打开登陆页\n" +
                "  -Remove     从代理例外列表中移除该地址\n" +
                "  -About      显示本对话框\n" +
                "\n" +
                "Copyright © LHStudio 2026";
            MessageBox.Show(text, "关于 Unit Login Launcher",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void UpdateProxyBypass(bool remove)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
            {
                if (key == null)
                {
                    throw new Exception("Could not find the current user's Windows Internet proxy settings.");
                }

                string existing = (string)key.GetValue("ProxyOverride", "");
                if (existing == null) existing = "";

                List<string> updated = new List<string>();
                string pattern = @"^(\*\.)?" + Regex.Escape(HostAddress) + @"(:\d+)?$";
                foreach (string part in existing.Split(';'))
                {
                    string entry = part.Trim();
                    if (entry.Length > 0 && !Regex.IsMatch(entry, pattern))
                    {
                        updated.Add(entry);
                    }
                }

                if (!remove)
                {
                    updated.Add(HostAddress);
                }

                key.SetValue("ProxyOverride", string.Join(";", updated.ToArray()), RegistryValueKind.String);
            }

            RefreshProxySettings();
        }

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        private static void RefreshProxySettings()
        {
            InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0); // INTERNET_OPTION_SETTINGS_CHANGED
            InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0); // INTERNET_OPTION_REFRESH
        }

        private static void LaunchChrome()
        {
            string[] candidates = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Google\Chrome\Application\chrome.exe")
            };

            string chrome = null;
            foreach (string candidate in candidates)
            {
                if (candidate.Length > 0 && File.Exists(candidate))
                {
                    chrome = candidate;
                    break;
                }
            }

            if (chrome == null)
            {
                throw new Exception("Google Chrome was not found.");
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = chrome;
            startInfo.WorkingDirectory = Path.GetDirectoryName(chrome);
            startInfo.Arguments = "--new-window " + Url;
            Process.Start(startInfo);
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("单位内网登陆工具")]
[assembly: AssemblyDescription("绕过系统代理与 Clash 虚拟网卡(TUN)直连单位登陆服务器,并用 Chrome 打开登陆页")]
[assembly: AssemblyCompany("LHStudio")]
[assembly: AssemblyProduct("Unit Login Launcher")]
[assembly: AssemblyCopyright("Copyright © LHStudio 2026")]
[assembly: AssemblyTrademark("LHStudio")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: AssemblyInformationalVersion("1.1.0.0")]

namespace UnitLogin
{
    internal static class Program
    {
        private const string HostAddress = "115.24.187.161";
        private const string Url = "http://115.24.187.161/";
        private const string ProductTitle = "Unit login launcher";

        // 直连路由:Clash 虚拟网卡(TUN)模式通过 0.0.0.0/1 + 128.0.0.0/1 两条路由劫持全部流量,
        // 更精确的主机/网段路由按最长前缀匹配优先,可使这些流量直接走物理网关。
        // 非持久路由(重启自动消失),由本工具每次运行时按需重新添加。
        private static readonly string[] DirectRoutes = new string[] { "115.24.187.161/32", "115.24.0.0/16" };

        private static readonly Regex TunNamePattern = new Regex(
            "(^|[^a-z])(meta|clash|mihomo|singbox|sing-box|wintun|wireguard|openvpn|softether|zerotier|tailscale|tun|tap)([^a-z]|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        [STAThread]
        private static int Main(string[] args)
        {
            bool remove = false;
            bool about = false;
            bool direct = false;
            bool noRoute = false;
            bool preview = false;
            string helperMode = null;
            bool helperPending = false;
            foreach (string arg in args)
            {
                string flag = arg.Trim().TrimStart('-', '/').ToLowerInvariant();
                if (helperPending)
                {
                    helperMode = flag;
                    helperPending = false;
                    continue;
                }
                if (flag == "remove") remove = true;
                if (flag == "about") about = true;
                if (flag == "direct") direct = true;
                if (flag == "noroute") noRoute = true;
                if (flag == "preview") preview = true;
                if (flag == "routehelper") helperPending = true;
            }

            if (about)
            {
                ShowAbout();
                return 0;
            }

            if (helperMode != null)
            {
                return RunRouteHelper(helperMode);
            }
            if (helperPending)
            {
                MessageBox.Show("Route helper mode requires an argument (add or remove).",
                    ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            if (preview)
            {
                try
                {
                    WritePreview();
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                    return 1;
                }
                return 0;
            }

            try
            {
                UpdateProxyBypass(remove);

                if (remove)
                {
                    string note = RemoveDirectRoutesIfPresent();
                    MessageBox.Show("Removed " + HostAddress + " from the proxy bypass list." + note,
                        ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                bool routeAdded = AddRoutesIfNeeded(direct, noRoute);
                LaunchChrome();

                if (routeAdded)
                {
                    MessageBox.Show(
                        "已添加直连路由:登陆服务器与校园网段将绕过 Clash 虚拟网卡(TUN)直连。\n\n" +
                        "若仍无法连接 TJNU,可能是校园网检测代理软件本身,请暂时退出 Clash 后重试。",
                        ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return 0;
            }
            catch (Exception ex)
            {
                ShowError(ex);
                return 1;
            }
        }

        private static void ShowAbout()
        {
            string text =
                "单位内网登陆工具 (Unit Login Launcher)\n" +
                "版本 1.1.0.0    作者: LHStudio\n" +
                "\n" +
                "功能:\n" +
                "  1. 把登陆服务器 115.24.187.161 加入系统代理例外列表(直连);\n" +
                "  2. 检测到 Clash 虚拟网卡(TUN)劫持时,自动请求管理员权限添加直连路由,\n" +
                "     让登陆服务器与校园网段(115.24.0.0/16)绕过 TUN 直接走物理网关;\n" +
                "  3. 用 Chrome 新窗口打开登陆页 http://115.24.187.161/\n" +
                "\n" +
                "命令行参数:\n" +
                "  (无参数)     添加代理例外,按需添加直连路由,并打开登陆页\n" +
                "  -Direct      强制添加直连路由(即使未检测到 TUN 劫持)\n" +
                "  -NoRoute     跳过直连路由步骤(不弹 UAC)\n" +
                "  -Remove      移除代理例外并清理直连路由\n" +
                "  -Preview     调试预览:写出 debug-preview.txt(不修改任何设置)\n" +
                "  -About       显示本对话框\n" +
                "\n" +
                "提示: 若添加直连路由后仍无法连接校园网,可能是校园网检测代理软件本身,\n" +
                "      请暂时退出 Clash 后再连接。\n" +
                "\n" +
                "Copyright © LHStudio 2026";
            MessageBox.Show(text, "关于 Unit Login Launcher",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void ShowError(Exception ex)
        {
            string message = ex.Message;
            if (string.IsNullOrEmpty(message)) message = ex.GetType().Name;
            MessageBox.Show(message, ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        // 返回 true 表示本次确实添加了直连路由(用于决定是否弹出后续提示)。
        private static bool AddRoutesIfNeeded(bool direct, bool noRoute)
        {
            if (noRoute)
            {
                return false;
            }

            bool tunDetected = IsTunHijackActive();
            if (!direct && !tunDetected)
            {
                return false;
            }

            if (IsElevated())
            {
                string error = AddDirectRoutes();
                if (error != null)
                {
                    MessageBox.Show(error, ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                return true;
            }

            int exitCode;
            if (RelaunchElevated("-RouteHelper add", out exitCode))
            {
                if (exitCode != 0)
                {
                    return false; // 辅助进程失败时已自行弹窗显示错误
                }
                return true;
            }

            MessageBox.Show(
                "已取消管理员授权,未添加直连路由,登陆页可能仍打不开。\n" +
                "可重新运行本工具并同意授权,或在 Clash 中暂时关闭虚拟网卡(TUN)模式。",
                ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // 返回附加到 -Remove 完成消息的说明;路由不存在时返回空字符串。
        private static string RemoveDirectRoutesIfPresent()
        {
            if (!DirectRoutesPresent())
            {
                return "";
            }

            if (IsElevated())
            {
                string error = DeleteDirectRoutes();
                if (error != null)
                {
                    MessageBox.Show(error, ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return "";
                }
                return "\n\n直连路由已删除。";
            }

            int exitCode;
            if (RelaunchElevated("-RouteHelper remove", out exitCode))
            {
                if (exitCode == 0)
                {
                    return "\n\n直连路由已删除。";
                }
                return ""; // 辅助进程失败时已自行弹窗显示错误
            }

            MessageBox.Show(
                "已取消管理员授权,直连路由未删除(重启计算机后它们也会自动失效)。",
                ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return "";
        }

        private static int RunRouteHelper(string mode)
        {
            if (!IsElevated())
            {
                MessageBox.Show("路由辅助模式必须以管理员权限运行。", ProductTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            string error;
            if (mode == "add")
            {
                error = AddDirectRoutes();
            }
            else if (mode == "remove")
            {
                error = DeleteDirectRoutes();
            }
            else
            {
                error = "未知的路由辅助参数: " + mode;
            }

            if (error != null)
            {
                MessageBox.Show(error, ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            return 0;
        }

        private static string AddDirectRoutes()
        {
            string gateway;
            int interfaceIndex;
            string adapterName;
            if (!TryGetPhysicalGateway(out gateway, out interfaceIndex, out adapterName))
            {
                return "未找到可用的物理网关,无法添加直连路由。\n" +
                    "请先确认已连接到 TJNU(或其它网络)后再运行本工具。";
            }

            StringBuilder errors = new StringBuilder();
            foreach (string cidr in DirectRoutes)
            {
                string destination;
                string mask;
                ParseCidr(cidr, out destination, out mask);

                RunRoute("delete " + destination); // 清理旧条目,失败(不存在)时忽略

                int exitCode;
                string output = RunRoute("add " + destination + " mask " + mask + " " + gateway +
                    " METRIC 1 IF " + interfaceIndex, out exitCode);
                if (exitCode != 0)
                {
                    errors.AppendLine("route add " + destination + " 失败 (exit " + exitCode + "):");
                    errors.AppendLine(string.IsNullOrEmpty(output) ? "(无输出)" : output.Trim());
                }
            }

            if (errors.Length > 0)
            {
                return errors.ToString();
            }
            return null;
        }

        private static string DeleteDirectRoutes()
        {
            StringBuilder errors = new StringBuilder();
            foreach (string cidr in DirectRoutes)
            {
                string destination;
                string mask;
                ParseCidr(cidr, out destination, out mask);

                int exitCode;
                string output = RunRoute("delete " + destination, out exitCode);
                if (exitCode != 0)
                {
                    errors.AppendLine("route delete " + destination + " 失败 (exit " + exitCode + "):");
                    errors.AppendLine(string.IsNullOrEmpty(output) ? "(无输出)" : output.Trim());
                }
            }

            if (errors.Length > 0)
            {
                return errors.ToString();
            }
            return null;
        }

        // TUN 劫持特征:wintun 式半默认路由 0.0.0.0/1 与 128.0.0.0/1;或存在名称可疑的虚拟网卡。
        private static bool IsTunHijackActive()
        {
            if (RouteEntryExists("0.0.0.0", "128.0.0.0") || RouteEntryExists("128.0.0.0", "128.0.0.0"))
            {
                return true;
            }
            return HasTunLikeAdapter();
        }

        private static bool RouteEntryExists(string destination, string mask)
        {
            string table = GetRouteTable();
            foreach (string line in table.Split('\n'))
            {
                string[] parts = line.Trim().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                // 活动路由行有 5 列(目标 掩码 网关 接口 跳数);持久路由段只有 3 列,接口列表与标题行不会以这两个 IP 开头。
                if (parts.Length >= 4 && parts[0] == destination && parts[1] == mask)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool DirectRoutesPresent()
        {
            foreach (string cidr in DirectRoutes)
            {
                string destination;
                string mask;
                ParseCidr(cidr, out destination, out mask);
                if (RouteEntryExists(destination, mask))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasTunLikeAdapter()
        {
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (TunNamePattern.IsMatch(ni.Name + " " + ni.Description))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private class AdapterCandidate
        {
            public NetworkInterface Adapter;
            public string Gateway;            // 网卡配置里的首个有效 IPv4 网关
            public List<string> UnicastIPv4;  // 网卡上的 IPv4 地址(用于和路由表的接口列对上)
            public int Index;
            public bool IsWireless;
        }

        // 收集"真实的"联网网卡:只认以太网/Wi-Fi 类型、排除名称可疑的虚拟网卡、
        // 必须配有 IPv4 网关和地址。VMware/Hyper-V 等虚拟网卡虽然类型也是以太网,
        // 但主机侧不配网关,会被自然排除。
        private static List<AdapterCandidate> CollectAdapterCandidates()
        {
            List<AdapterCandidate> result = new List<AdapterCandidate>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Wireless80211) continue;
                    if (TunNamePattern.IsMatch(ni.Name + " " + ni.Description)) continue;

                    IPInterfaceProperties props;
                    try
                    {
                        props = ni.GetIPProperties();
                    }
                    catch
                    {
                        continue;
                    }
                    if (props == null) continue;

                    AdapterCandidate item = new AdapterCandidate();
                    item.Adapter = ni;
                    item.IsWireless = ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
                    item.UnicastIPv4 = new List<string>();
                    if (props.UnicastAddresses != null)
                    {
                        foreach (UnicastIPAddressInformation ua in props.UnicastAddresses)
                        {
                            if (ua == null || ua.Address == null) continue;
                            if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                            string text = ua.Address.ToString();
                            if (text.StartsWith("169.254.")) continue;
                            item.UnicastIPv4.Add(text);
                        }
                    }

                    if (props.GatewayAddresses != null)
                    {
                        foreach (GatewayIPAddressInformation gw in props.GatewayAddresses)
                        {
                            if (gw == null || gw.Address == null) continue;
                            if (gw.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                            string text = gw.Address.ToString();
                            if (text.StartsWith("0.") || text.StartsWith("169.254.")) continue;
                            item.Gateway = text;
                            break;
                        }
                    }

                    if (item.Gateway == null || item.UnicastIPv4.Count == 0) continue;

                    try
                    {
                        IPv4InterfaceProperties v4 = props.GetIPv4Properties();
                        if (v4 == null || v4.Index <= 0) continue;
                        item.Index = v4.Index;
                    }
                    catch
                    {
                        continue;
                    }

                    result.Add(item);
                }
            }
            catch
            {
            }
            return result;
        }

        private static bool IsLiteralIpv4(string text)
        {
            IPAddress address;
            if (!IPAddress.TryParse(text, out address)) return false;
            return address.AddressFamily == AddressFamily.InterNetwork;
        }

        // 选定加路由用的网卡:首选路由表中活动默认路由(0.0.0.0/0.0.0.0)里
        // 跃点数最小、且出口接口 IP 属于候选网卡的那条(操作系统的选择最可靠);
        // 一条默认路由都没有时,退回"优先无线"的启发式。
        private static bool TryGetPhysicalGateway(out string gateway, out int interfaceIndex, out string adapterName)
        {
            gateway = null;
            interfaceIndex = 0;
            adapterName = null;

            List<AdapterCandidate> candidates = CollectAdapterCandidates();
            if (candidates.Count == 0)
            {
                return false;
            }

            AdapterCandidate matched = null;
            string matchedGateway = null;
            int bestMetric = int.MaxValue;
            foreach (string line in GetRouteTable().Split('\n'))
            {
                string[] parts = line.Trim().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;
                if (parts[0] != "0.0.0.0" || parts[1] != "0.0.0.0") continue;
                if (!IsLiteralIpv4(parts[2]) || !IsLiteralIpv4(parts[3])) continue;
                int metric;
                if (!int.TryParse(parts[4], out metric)) continue;

                foreach (AdapterCandidate item in candidates)
                {
                    if (!item.UnicastIPv4.Contains(parts[3])) continue;
                    if (metric < bestMetric)
                    {
                        bestMetric = metric;
                        matched = item;
                        matchedGateway = parts[2];
                    }
                    break;
                }
            }

            if (matched != null)
            {
                gateway = matchedGateway;
                interfaceIndex = matched.Index;
                adapterName = matched.Adapter.Name;
                return true;
            }

            AdapterCandidate best = null;
            foreach (AdapterCandidate item in candidates)
            {
                if (best == null || (item.IsWireless && !best.IsWireless))
                {
                    best = item;
                }
            }
            gateway = best.Gateway;
            interfaceIndex = best.Index;
            adapterName = best.Adapter.Name;
            return true;
        }

        private static void ParseCidr(string cidr, out string destination, out string mask)
        {
            int slash = cidr.IndexOf('/');
            if (slash < 0)
            {
                destination = cidr;
                mask = "255.255.255.255";
                return;
            }

            destination = cidr.Substring(0, slash);
            int bits = int.Parse(cidr.Substring(slash + 1));
            uint value = 0xFFFFFFFFu;
            if (bits < 32)
            {
                value = value << (32 - bits);
            }
            mask = ((value >> 24) & 0xFF) + "." + ((value >> 16) & 0xFF) + "." +
                ((value >> 8) & 0xFF) + "." + (value & 0xFF);
        }

        private static string GetRouteTable()
        {
            string output = RunRoute("print -4");
            return output == null ? "" : output;
        }

        private static string RunRoute(string arguments)
        {
            int exitCode;
            return RunRoute(arguments, out exitCode);
        }

        private static string RunRoute(string arguments, out int exitCode)
        {
            exitCode = -1;
            string routeExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "route.exe");
            if (!File.Exists(routeExe))
            {
                routeExe = "route.exe";
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = routeExe;
            startInfo.Arguments = arguments;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = Encoding.Default;
            startInfo.StandardErrorEncoding = Encoding.Default;

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                    return output;
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static bool IsElevated()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool RelaunchElevated(string arguments, out int exitCode)
        {
            exitCode = -1;

            string exePath = null;
            try
            {
                exePath = Assembly.GetExecutingAssembly().Location;
            }
            catch
            {
            }
            if (string.IsNullOrEmpty(exePath))
            {
                try
                {
                    exePath = Process.GetCurrentProcess().MainModule.FileName;
                }
                catch
                {
                }
            }
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                MessageBox.Show("无法定位本程序文件,无法请求管理员权限。", ProductTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = exePath;
            startInfo.Arguments = arguments;
            startInfo.UseShellExecute = true;
            startInfo.Verb = "runas";

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return false;
                    }
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                    return true;
                }
            }
            catch (Win32Exception)
            {
                return false; // 用户取消 UAC 或提权失败
            }
        }

        private static void WritePreview()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("==== Unit Login Launcher 调试预览(只读,未修改任何设置) ====");
            sb.AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("当前是否管理员: " + (IsElevated() ? "是" : "否"));
            sb.AppendLine("TUN 劫持特征(0.0.0.0/1 或 128.0.0.0/1): " +
                (RouteEntryExists("0.0.0.0", "128.0.0.0") || RouteEntryExists("128.0.0.0", "128.0.0.0")));
            sb.AppendLine("疑似 Clash/TUN 网卡在线: " + HasTunLikeAdapter());
            sb.AppendLine("直连路由是否已存在: " + DirectRoutesPresent());
            sb.AppendLine();

            string gateway;
            int interfaceIndex;
            string adapterName;
            if (TryGetPhysicalGateway(out gateway, out interfaceIndex, out adapterName))
            {
                sb.AppendLine("选用物理网卡: " + adapterName);
                sb.AppendLine("网关: " + gateway + "    接口号: " + interfaceIndex);
            }
            else
            {
                sb.AppendLine("未找到可用物理网关(未联网,或只有虚拟网卡在线)");
            }
            sb.AppendLine();

            sb.AppendLine("==== 全部网卡 ====");
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    string gateways = "";
                    string addresses = "";
                    try
                    {
                        foreach (GatewayIPAddressInformation gw in ni.GetIPProperties().GatewayAddresses)
                        {
                            if (gw != null && gw.Address != null)
                            {
                                gateways += (gateways.Length > 0 ? ", " : "") + gw.Address;
                            }
                        }
                        foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ua != null && ua.Address != null &&
                                ua.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                addresses += (addresses.Length > 0 ? ", " : "") + ua.Address;
                            }
                        }
                    }
                    catch
                    {
                    }
                    sb.AppendLine("[" + ni.NetworkInterfaceType + "] " + ni.OperationalStatus +
                        "  " + ni.Name + "  (" + ni.Description + ")");
                    sb.AppendLine("    地址: " + (addresses.Length > 0 ? addresses : "无") +
                        "    网关: " + (gateways.Length > 0 ? gateways : "无"));
                }
            }
            catch
            {
            }
            sb.AppendLine();

            sb.AppendLine("==== 将执行的路由命令(需管理员权限) ====");
            foreach (string cidr in DirectRoutes)
            {
                string destination;
                string mask;
                ParseCidr(cidr, out destination, out mask);
                sb.AppendLine("route delete " + destination);
                sb.AppendLine("route add " + destination + " mask " + mask + " " +
                    (gateway == null ? "<物理网关>" : gateway) + " METRIC 1 IF " +
                    (interfaceIndex > 0 ? interfaceIndex.ToString() : "<接口号>"));
            }
            sb.AppendLine();

            sb.AppendLine("==== route print -4 原始输出 ====");
            sb.AppendLine(GetRouteTable());

            string path = Path.Combine(AppFolder(), "debug-preview.txt");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("调试信息已写入:\n" + path, ProductTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string AppFolder()
        {
            try
            {
                string location = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(location))
                {
                    return Path.GetDirectoryName(location);
                }
            }
            catch
            {
            }
            return AppDomain.CurrentDomain.BaseDirectory;
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

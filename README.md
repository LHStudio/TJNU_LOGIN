# 单位内网登陆工具 (Unit Login Launcher)

**作者: LHStudio** · 版本 1.1.0.0 · Windows 10/11 · 单文件绿色程序

## 简介

本机挂了代理(Clash 系统代理 / 虚拟网卡 TUN 模式)时,校园网 **TJNU** 会连不上或登陆页打不开。双击本工具即可一键完成:

1. 把登陆服务器 `115.24.187.161` 加入系统代理例外列表(直连),解决**系统代理模式**下的拦截;
2. 检测到 **Clash 虚拟网卡(TUN)劫持**时,自动请求管理员权限添加**直连路由**,让登陆服务器和校园网段绕过 TUN 直接走物理网关(会弹一次 UAC);
3. 用 Chrome 新窗口打开登陆页 `http://115.24.187.161/`。

## 功能说明

1. 把 `115.24.187.161` 加入当前用户的代理例外列表(注册表 `ProxyOverride`),**原有例外项(localhost、内网网段等)全部保留**,且不会重复添加;
2. 检测 TUN 劫持:解析路由表,发现 `0.0.0.0/1`、`128.0.0.0/1` 特征路由(或 Meta/Clash/Mihomo/Wintun 等虚拟网卡在线)即判定为 TUN 劫持;
3. 判定被劫持时添加两条**非持久**直连路由(重启自动消失,由工具每次运行时按需重加,不留残留):
   - `115.24.187.161/32` — 登陆服务器主机路由;
   - `115.24.0.0/16` — 校园网段;
4. 调用系统接口刷新代理设置,立即生效,无需重启浏览器;
5. 自动定位 Chrome(依次查找 Program Files → Program Files (x86) → 用户目录),以新窗口打开登陆页。

> **重要提示**:若添加直连路由后仍无法连接 TJNU,说明校园网可能在**检测代理软件本身**(反代理检测),此时任何路由都无效,请**暂时退出 Clash** 后再连接校园网,登陆完成后再打开 Clash。

## 使用方法

| 方式 | 效果 |
|---|---|
| 双击 `TJNU_Open-UnitLogin.exe` | 添加代理例外;若检测到 TUN 劫持则弹 UAC 添加直连路由;打开登陆页 |
| `TJNU_Open-UnitLogin.exe -Direct` | 强制添加直连路由(即使未检测到 TUN,便于排障) |
| `TJNU_Open-UnitLogin.exe -NoRoute` | 只加代理例外并打开登陆页,跳过路由步骤(Clash 没开时用,不弹 UAC) |
| `TJNU_Open-UnitLogin.exe -Remove` | 移除代理例外;若直连路由存在则一并清理(需要时会弹 UAC) |
| `TJNU_Open-UnitLogin.exe -Preview` | 调试预览:把检测到的网卡/网关/TUN 状态/将执行的命令写入 `debug-preview.txt`,**不修改任何设置** |
| `TJNU_Open-UnitLogin.exe -About` | 显示"关于"对话框(版本 / 作者 / 用法) |

- 添加路由需要管理员权限:工具会重启自身提权,代理例外(HKCU)部分始终无需管理员;
- 出错时弹窗提示(例如找不到 Chrome、用户取消 UAC);
- 参数不区分大小写,`-Remove`、`--remove`、`/Remove` 均可。

## 绕过 Clash 虚拟网卡(TUN)的原理

系统代理模式只影响应用层,把 IP 写进 `ProxyOverride` 即可直连;但 TUN 模式是 Clash 在**路由表**层面用 `0.0.0.0/1` + `128.0.0.0/1` 两条路由把全机流量劫持进虚拟网卡,代理例外列表完全管不到。

对策:添加指向**物理网关**的更精确路由(主机路由 `/32`、网段路由 `/16`)。IPv4 按**最长前缀匹配**选路,`/32` 与 `/16` 永远优先于 TUN 的 `/1` 路由,于是发往登陆服务器和校园网的流量直接从物理网卡出去,不再经过 Clash。

工具会自动识别物理网卡(只认以太网/Wi-Fi 类型、排除 Meta/Clash/Mihomo/Wintun 等名称的虚拟网卡、有网关者优先无线),取其网关和接口号执行 `route add ... METRIC 1 IF <接口号>`。

## 排障

1. 运行 `TJNU_Open-UnitLogin.exe -Preview`,用记事本打开生成的 `debug-preview.txt`:
   - 看「TUN 劫持特征」「疑似 Clash/TUN 网卡」是否为 True(开着 Clash TUN 时应至少一个为 True);
   - 看「选用物理网卡/网关/接口号」是否指向你连 TJNU 的那块网卡;
   - 看「route print -4 原始输出」确认实际路由。
2. 若检测正常但加了路由仍连不上 → 校园网反代理检测,**暂时退出 Clash** 再试;
3. 直连路由为非持久路由,重启后自动消失,无需手动清理;也可随时用 `-Remove` 清理。

## 作者信息(如何在程序中看到 LHStudio)

- **右键 exe → 属性 → 详细信息**:公司 = `LHStudio`,版权 = `Copyright © LHStudio 2026`,产品名称 = `Unit Login Launcher`,文件说明 = `单位内网登陆工具`,版本 = `1.1.0.0`;
- **运行 `TJNU_Open-UnitLogin.exe -About`**:弹出"关于"对话框,含作者署名;
- 说明:Windows 对 .exe 文件的属性页不提供单独的"作者"一行(系统固定行为,notepad.exe、chrome.exe 等微软/谷歌官方程序同样没有),"**公司**"字段就是 exe 的作者署名位,已设为 LHStudio。

## 文件清单

| 文件 | 说明 |
|---|---|
| `TJNU_Open-UnitLogin.exe` | 主程序,双击运行 |
| `Open-UnitLogin.cs` | 主程序源码(C#) |
| `Open-UnitLogin.ico` | 程序图标(蓝底白钥匙,含 256/48/32/16 四种尺寸) |
| `MakeIcon.cs` | 图标生成器源码 |
| `README.md` | 本文档 |
| `Open-UnitLogin.ps1` / `.vbs` | 旧版脚本,已被 exe 完全取代,可删除 |
| `debug-preview.txt` | 仅运行 `-Preview` 时生成的调试文件,可随时删除 |

## 自定义与重新编译

### 修改目标地址 / 直连网段

编辑 `Open-UnitLogin.cs` 顶部的常量后重新编译:

```csharp
private const string HostAddress = "115.24.187.161";
private const string Url = "http://115.24.187.161/";
private static readonly string[] DirectRoutes = new string[] { "115.24.187.161/32", "115.24.0.0/16" };
```

### 重新编译主程序

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo -target:winexe -platform:anycpu -optimize+ -codepage:65001 -r:System.dll -r:System.Windows.Forms.dll -win32icon:Open-UnitLogin.ico -out:TJNU_Open-UnitLogin.exe Open-UnitLogin.cs
```

> `-codepage:65001` 必须保留:源码为 UTF-8 编码且含中文,缺少该参数会乱码。

### 更换图标

修改 `MakeIcon.cs` 中的配色或图形后,执行:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo -r:System.Drawing.dll -out:MakeIcon.exe MakeIcon.cs
MakeIcon.exe
```

会生成新的 `Open-UnitLogin.ico`,再按上面命令重新编译主程序即可。

## 技术细节

- 单文件 exe 约 28 KB,AnyCPU,无需安装任何依赖(仅依赖 Windows 自带的 .NET Framework 4.x);
- 代理例外写入位置:`HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings` 的 `ProxyOverride` 值(REG_SZ,分号分隔);
- 添加前会先移除同主机旧条目(兼容 `*.主机`、`主机:端口` 写法),避免重复;
- 生效方式:调用 WinINET 的 `InternetSetOption`(SETTINGS_CHANGED + REFRESH)通知所有应用;
- 直连路由:`route.exe` 添加**非持久**路由(不加 `-p`,重启自动消失);添加前先 `route delete` 清理旧条目避免"对象已存在";TUN 检测基于路由表 `0.0.0.0/128.0.0.0`、`128.0.0.0/128.0.0.0` 特征与虚拟网卡名称;
- 提权方式:检测到需要加路由但当前未提权时,以 `runas` 重启自身进入 `-RouteHelper add|remove` 辅助模式,该实例只负责路由操作,不打开浏览器(避免 Chrome 被提权)。

## 版本历史

- **1.1.0.0**(2026-10-18):新增**绕过 Clash 虚拟网卡(TUN)直连**功能——检测到 TUN 劫持时自动提权添加物理网关直连路由(`115.24.187.161/32`、`115.24.0.0/16`,非持久);新增 `-Direct`、`-NoRoute`、`-Preview` 参数;`-Remove` 现在会同时清理直连路由;主程序更名为 `TJNU_Open-UnitLogin.exe`。
- **1.0.0.0**(2026-08-29):首个 exe 版本,由 PowerShell + VBS 脚本改写为 C# 单文件程序;内嵌图标与版本信息;支持 `-Remove`、`-About` 参数。

## 版权

Copyright © LHStudio 2026. 仅限内部使用。

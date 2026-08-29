# TJNU_LOGIN
天津师范大学校园网连接跳转工具
# 内网登陆工具 (Unit Login Launcher)

**作者: LHStudio** · 版本 1.0.0.0 · Windows 10/11 · 单文件绿色程序

## 简介

本机挂了代理(全局/PAC)时,访问单位内网登陆页会被代理拦截。双击本工具即可一键完成:把登陆服务器 `115.24.187.161` 加入系统代理例外列表(直连),再用 Chrome 新窗口打开登陆页 `http://115.24.187.161/`。

## 功能说明

1. 把 `115.24.187.161` 加入当前用户的代理例外列表(注册表 `ProxyOverride`),**原有例外项(localhost、内网网段等)全部保留**,且不会重复添加;
2. 调用系统接口刷新代理设置,立即生效,无需重启浏览器;
3. 自动定位 Chrome(依次查找 Program Files → Program Files (x86) → 用户目录),以新窗口打开登陆页。

## 使用方法

| 方式 | 效果 |
|---|---|
| 双击 `Open-UnitLogin.exe` | 添加代理例外并打开登陆页 |
| `Open-UnitLogin.exe -Remove` | 把该地址从例外列表移除(弹窗确认) |
| `Open-UnitLogin.exe -About` | 显示"关于"对话框(版本 / 作者 / 用法) |

- 无需管理员权限,只修改当前用户(HKCU)的设置;
- 出错时弹窗提示(例如找不到 Chrome);
- 参数不区分大小写,`-Remove`、`--remove`、`/Remove` 均可。


## 文件清单

| 文件 | 说明 |
|---|---|
| `Open-UnitLogin.exe` | 主程序,双击运行 |
| `Open-UnitLogin.cs` | 主程序源码(C#) |
| `Open-UnitLogin.ico` | 程序图标 |
| `MakeIcon.cs` | 图标生成器源码 |
| `README.md` | 本文档 |

## 自定义与重新编译

### 修改目标地址

编辑 `Open-UnitLogin.cs` 顶部的两个常量后重新编译:

```csharp
private const string HostAddress = "115.24.187.161";
private const string Url = "http://115.24.187.161/";
```

### 重新编译主程序

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo -target:winexe -platform:anycpu -optimize+ -codepage:65001 -r:System.dll -r:System.Windows.Forms.dll -win32icon:Open-UnitLogin.ico -out:Open-UnitLogin.exe Open-UnitLogin.cs
```

> `-codepage:65001` 必须保留:源码为 UTF-8 编码且含中文,缺少该参数会乱码。


## 技术细节

- 单文件 exe 约 19 KB,AnyCPU,无需安装任何依赖(仅依赖 Windows 自带的 .NET Framework 4.x);
- 代理例外写入位置:`HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings` 的 `ProxyOverride` 值(REG_SZ,分号分隔);
- 添加前会先移除同主机旧条目(兼容 `*.主机`、`主机:端口` 写法),避免重复;
- 生效方式:调用 WinINET 的 `InternetSetOption`(SETTINGS_CHANGED + REFRESH)通知所有应用。

## 版本历史

- **1.0.0.0**(2026-08-29):首个 exe 版本,由 PowerShell + VBS 脚本改写为 C# 单文件程序;内嵌图标与版本信息;支持 `-Remove`、`-About` 参数。

## 版权

Copyright © LHStudio 2026. MIT。

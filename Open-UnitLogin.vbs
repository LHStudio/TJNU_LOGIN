Option Explicit

Dim shell, fso, scriptDir, ps1Path, command, process, errorText

Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
ps1Path = fso.BuildPath(scriptDir, "Open-UnitLogin.ps1")

If Not fso.FileExists(ps1Path) Then
    MsgBox "Cannot find Open-UnitLogin.ps1 in the same folder." & vbCrLf & ps1Path, vbCritical, "Unit login launcher"
    WScript.Quit 1
End If

command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -NonInteractive -File " & Quote(ps1Path)
Set process = shell.Exec(command)

Do While process.Status = 0
    WScript.Sleep 100
Loop

If Not process.StdErr.AtEndOfStream Then
    errorText = process.StdErr.ReadAll
End If

If process.ExitCode <> 0 Then
    If errorText = "" Then errorText = "PowerShell exited with code " & process.ExitCode & "."
    MsgBox errorText, vbCritical, "Unit login launcher"
End If

Function Quote(value)
    Quote = Chr(34) & value & Chr(34)
End Function

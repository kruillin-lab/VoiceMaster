using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using VoiceMaster.DataClasses;
using VoiceMaster.Helper.Data;

namespace VoiceMaster.Helper.Functional;

internal static class CMDHelper
{
    internal static void CallCMD(EKEventId eventId ,string exePath, string command, string methodExtra)
    {
        try
        {
            var process = new Process();

            var args = Regex.Matches(command, @"[\""].+?[\""]|[^ ]+")
                             .Select(m => m.Value.Trim('"'))
                             .ToList();

            if (string.IsNullOrEmpty(exePath) && args.Count > 0)
            {
                process.StartInfo.FileName = args[0];
                args.RemoveAt(0);
            }
            else
            {
                process.StartInfo.FileName = exePath;
            }

            foreach (var arg in args)
                process.StartInfo.ArgumentList.Add(arg);

            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            LogHelper.Debug(MethodBase.GetCurrentMethod()!.Name + $" | {methodExtra}", @$"Calling command: '{exePath} {command}'", eventId);
            process.Start();

            while (!process.HasExited)
            {
                string output = process.StandardOutput.ReadLine();
                LogHelper.Debug(MethodBase.GetCurrentMethod()!.Name + $" | {methodExtra}", output, eventId);
            }
        }
        catch (Exception e)
        {
            LogHelper.Error(MethodBase.GetCurrentMethod()!.Name, e, eventId);
        }
    }

    internal static string CleanAnsi(string input)
    {
        return Regex.Replace(input, @"\x1B\[[0-9;]*[mK]", "").Replace(" ", "  ");
    }

    internal static void OpenUrl(string url)
    {
        if (Dalamud.Utility.Util.GetHostPlatform() == OSPlatform.Windows)
        {
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
    }
}

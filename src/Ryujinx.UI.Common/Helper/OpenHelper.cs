using Ryujinx.Common.Logging;
using System.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Ryujinx.UI.Common.Helper
{
    public static partial class OpenHelper
    {
        [LibraryImport("shell32.dll", SetLastError = true)]
        private static partial int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, IntPtr apidl, uint dwFlags);

        [LibraryImport("shell32.dll", SetLastError = true)]
        private static partial void ILFree(IntPtr pidlList);

        [LibraryImport("shell32.dll", SetLastError = true)]
        private static partial IntPtr ILCreateFromPathW([MarshalAs(UnmanagedType.LPWStr)] string pszPath);

        public static void OpenFolder(string path)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath) && IsPathAllowed(fullPath) && IsWhitelisted(fullPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = fullPath,
                        UseShellExecute = false,
                    });
                }
                else
                {
                    Logger.Notice.Print(LogClass.Application, $"Directory \"{path}\" doesn't exist or is not allowed!");
                }
            }
            catch (Exception ex)
            {
                Logger.Error.Print(LogClass.Application, $"Failed to open directory \"{path}\": {ex.Message}");
            }
        }

        public static void LocateFile(string path)
        {
            if (File.Exists(path))
            {
                if (OperatingSystem.IsWindows())
                {
                    IntPtr pidlList = ILCreateFromPathW(path);
                    if (pidlList != IntPtr.Zero)
                    {
                        try
                        {
                            Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(pidlList, 0, IntPtr.Zero, 0));
                        }
                        finally
                        {
                            ILFree(pidlList);
                        }
                    }
                }
                else if (OperatingSystem.IsMacOS())
                {
                    ObjectiveC.NSString nsStringPath = new(path);
                    ObjectiveC.Object nsUrl = new("NSURL");
                    var urlPtr = nsUrl.GetFromMessage("fileURLWithPath:", nsStringPath);

                    ObjectiveC.Object nsArray = new("NSArray");
                    ObjectiveC.Object urlArray = nsArray.GetFromMessage("arrayWithObject:", urlPtr);

                    ObjectiveC.Object nsWorkspace = new("NSWorkspace");
                    ObjectiveC.Object sharedWorkspace = nsWorkspace.GetFromMessage("sharedWorkspace");

                    sharedWorkspace.SendMessage("activateFileViewerSelectingURLs:", urlArray);
                }
                else if (OperatingSystem.IsLinux())
                {
                    Process.Start("dbus-send", $"--session --print-reply --dest=org.freedesktop.FileManager1 --type=method_call /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:\"file://{path}\" string:\"\"");
                }
                else
                {
                    OpenFolder(Path.GetDirectoryName(path));
                }
            }
            else
            {
                Logger.Notice.Print(LogClass.Application, $"File \"{path}\" doesn't exist!");
            }
        }

        public static void OpenUrl(string url)
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}"));
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", url);
            }
            else if (OperatingSystem.IsMacOS())
            {
                ObjectiveC.NSString nsStringPath = new(url);
                ObjectiveC.Object nsUrl = new("NSURL");
                var urlPtr = nsUrl.GetFromMessage("URLWithString:", nsStringPath);

                ObjectiveC.Object nsWorkspace = new("NSWorkspace");
                ObjectiveC.Object sharedWorkspace = nsWorkspace.GetFromMessage("sharedWorkspace");

                sharedWorkspace.GetBoolFromMessage("openURL:", urlPtr);
            }
            else
            {
                Logger.Notice.Print(LogClass.Application, $"Cannot open url \"{url}\" on this platform!");
            }
        }
        private static bool IsPathAllowed(string path)
        {
            string[] allowedDirectories = { "C:\\AllowedPath1", "C:\\AllowedPath2" }; // Add allowed directories here
            return allowedDirectories.Any(allowedDir => path.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase));
        }
        private static bool IsWhitelisted(string path)
        {
            // Define a list of allowed directories
            string[] allowedDirectories = { "C:\\AllowedDir1", "C:\\AllowedDir2" };
            return allowedDirectories.Any(allowedDir => path.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase));
        }
    }
}

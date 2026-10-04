using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RemoveFileReadonlyAttribute.WPF
{
    /// <summary>
    /// Vista 风格的文件夹选择对话框（基于 COM IFileOpenDialog + FOS_PICKFOLDERS）。
    /// </summary>
    internal static class FolderPicker
    {
        /// <summary>
        /// 弹出文件夹选择框。用户取消或失败时返回 null。
        /// </summary>
        /// <param name="owner">宿主窗口。</param>
        /// <param name="initialFolder">对话框初始定位的文件夹，不存在则忽略。</param>
        public static string? PickFolder(Window owner, string initialFolder)
        {
            var dialog = (IFileOpenDialog)new FileOpenDialogRCW();
            try
            {
                dialog.GetOptions(out var options);
                dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHONLY);

                if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder))
                {
                    try
                    {
                        SHCreateItemFromParsingName(
                            initialFolder, IntPtr.Zero, typeof(IShellItem).GUID, out var initialItem);
                        dialog.SetFolder(initialItem);
                    }
                    catch
                    {
                        // 初始文件夹设置失败不影响对话框正常弹出
                    }
                }

                var ownerHandle = new WindowInteropHelper(owner).Handle;
                if (dialog.Show(ownerHandle) != 0) // S_OK 以外的值视为取消
                {
                    return null;
                }

                dialog.GetResult(out var item);
                item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var pathPtr);
                try
                {
                    return Marshal.PtrToStringUni(pathPtr);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pathPtr);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(dialog);
            }
        }

        private const uint FOS_PICKFOLDERS = 0x00000020;
        private const uint FOS_FORCEFILESYSTEM = 0x00000040;
        private const uint FOS_PATHONLY = 0x00000200;

        private enum SIGDN : uint
        {
            SIGDN_FILESYSPATH = 0x80058000,
        }

        [ComImport]
        [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialogRCW
        {
        }

        [ComImport]
        [Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] uint Show(IntPtr hwndOwner);
            [PreserveSig] uint SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            [PreserveSig] uint SetFileTypeIndex(uint iFileType);
            [PreserveSig] uint GetFileTypeIndex(out uint piFileType);
            [PreserveSig] uint Advise(IntPtr pfde, out uint pdwCookie);
            [PreserveSig] uint Unadvise(uint dwCookie);
            [PreserveSig] uint SetOptions(uint fos);
            [PreserveSig] uint GetOptions(out uint pfos);
            [PreserveSig] uint SetDefaultFolder(IShellItem psi);
            [PreserveSig] uint SetFolder(IShellItem psi);
            [PreserveSig] uint GetFolder(out IShellItem ppsi);
            [PreserveSig] uint GetCurrentSelection(out IShellItem ppsi);
            [PreserveSig] uint SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            [PreserveSig] uint GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            [PreserveSig] uint SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            [PreserveSig] uint SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            [PreserveSig] uint SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            [PreserveSig] uint GetResult(out IShellItem ppsi);
            [PreserveSig] uint AddPlace(IShellItem psi, int fdap);
            [PreserveSig] uint SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            [PreserveSig] uint Close();
            [PreserveSig] uint SetClientGuid(in Guid guid);
            [PreserveSig] uint ClearClientData();
            [PreserveSig] uint SetFilter(IntPtr pFilter);
            [PreserveSig] uint GetResults(out IntPtr ppenum);
            [PreserveSig] uint GetSelectedItems(out IntPtr ppsai);
        }

        [ComImport]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            [PreserveSig] uint BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, out IntPtr ppv);
            [PreserveSig] uint GetParent(out IShellItem ppsi);
            [PreserveSig] uint GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
            [PreserveSig] uint GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            [PreserveSig] uint Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            in Guid riid,
            out IShellItem ppv);
    }
}

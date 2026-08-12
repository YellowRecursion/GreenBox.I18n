using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GreenBox.I18n.Editor.Host.Infrastructure;

/// <summary>
/// Opens the operating system file picker for a localization catalog.
/// </summary>
public sealed class CatalogFilePicker
{
    /// <summary>
    /// Lets the user select a catalog and returns its absolute path.
    /// </summary>
    /// <returns>The selected path, or <see langword="null"/> when cancelled.</returns>
    public Task<string?> PickAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The native catalog picker is not available on this operating system yet.");
        }

        return StartWindowsPicker();
    }

    [SupportedOSPlatform("windows")]
    private static Task<string?> StartWindowsPicker()
    {
        nint ownerWindow = GetForegroundWindow();
        var completion = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(PickOnWindows(ownerWindow));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "GreenBox I18n catalog picker",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    [SupportedOSPlatform("windows")]
    private static string? PickOnWindows(nint ownerWindow)
    {
        Type dialogType = Type.GetTypeFromCLSID(
            new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7"),
            throwOnError: true)!;
        var dialog = (IFileOpenDialog)Activator.CreateInstance(dialogType)!;
        try
        {
            dialog.SetTitle("Open localization catalog");
            dialog.SetFileTypes(1,
            [
                new FileDialogFilter("Localization catalog (*.json)", "*.json"),
            ]);
            dialog.SetDefaultExtension("json");
            dialog.SetOptions(
                FileOpenOptions.ForceFileSystem |
                FileOpenOptions.PathMustExist |
                FileOpenOptions.FileMustExist);

            int result = dialog.Show(ownerWindow);
            if (result == HResultCancelled)
            {
                return null;
            }

            Marshal.ThrowExceptionForHR(result);
            dialog.GetResult(out IShellItem item);
            try
            {
                item.GetDisplayName(ShellItemDisplayName.FileSystemPath, out nint pathPointer);
                try
                {
                    return Marshal.PtrToStringUni(pathPointer);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pathPointer);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(item);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(dialog);
        }
    }

    private const int HResultCancelled = unchecked((int)0x800704C7);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint GetForegroundWindow();

    [Flags]
    private enum FileOpenOptions : uint
    {
        ForceFileSystem = 0x00000040,
        FileMustExist = 0x00001000,
        PathMustExist = 0x00000800,
    }

    private enum ShellItemDisplayName : uint
    {
        FileSystemPath = 0x80058000,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private readonly struct FileDialogFilter(string name, string pattern)
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public readonly string Name = name;

        [MarshalAs(UnmanagedType.LPWStr)]
        public readonly string Pattern = pattern;
    }

    [ComImport]
    [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig]
        int Show(nint parent);

        void SetFileTypes(
            uint fileTypeCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] FileDialogFilter[] filters);

        void SetFileTypeIndex(uint fileTypeIndex);
        void GetFileTypeIndex(out uint fileTypeIndex);
        void Advise(nint events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(FileOpenOptions options);
        void GetOptions(out FileOpenOptions options);
        void SetDefaultFolder(nint shellItem);
        void SetFolder(nint shellItem);
        void GetFolder(out nint shellItem);
        void GetCurrentSelection(out nint shellItem);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem shellItem);
        void AddPlace(nint shellItem, uint placement);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int result);
        void SetClientGuid(in Guid guid);
        void ClearClientData();
        void SetFilter(nint filter);
        void GetResults(out nint shellItems);
        void GetSelectedItems(out nint shellItems);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint bindingContext, in Guid handlerId, in Guid interfaceId, out nint result);
        void GetParent(out nint parent);
        void GetDisplayName(ShellItemDisplayName displayName, out nint name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }
}

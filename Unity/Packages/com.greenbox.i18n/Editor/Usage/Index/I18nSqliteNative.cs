#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace GreenBox.I18n.Usage.Index
{
    /// <summary>
    /// Declares the small native SQLite surface required by the usage index.
    /// </summary>
    internal static class I18nSqliteNative
    {
#if UNITY_EDITOR_WIN
        private const string LibraryName = "winsqlite3";
#elif UNITY_EDITOR_OSX
        private const string LibraryName = "libsqlite3.dylib";
#else
        private const string LibraryName = "libsqlite3.so.0";
#endif

        internal const int Ok = 0;
        internal const int Row = 100;
        internal const int Done = 101;

        [DllImport(LibraryName, EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Open(
            byte[] filename,
            out IntPtr database,
            int flags,
            IntPtr virtualFileSystem);

        [DllImport(LibraryName, EntryPoint = "sqlite3_close_v2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Close(IntPtr database);

        [DllImport(LibraryName, EntryPoint = "sqlite3_busy_timeout", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int BusyTimeout(IntPtr database, int milliseconds);

        [DllImport(LibraryName, EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Execute(
            IntPtr database,
            byte[] sql,
            IntPtr callback,
            IntPtr callbackArgument,
            out IntPtr errorMessage);

        [DllImport(LibraryName, EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Prepare(
            IntPtr database,
            byte[] sql,
            int byteCount,
            out IntPtr statement,
            IntPtr tail);

        [DllImport(LibraryName, EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Step(IntPtr statement);

        [DllImport(LibraryName, EntryPoint = "sqlite3_reset", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Reset(IntPtr statement);

        [DllImport(LibraryName, EntryPoint = "sqlite3_clear_bindings", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ClearBindings(IntPtr statement);

        [DllImport(LibraryName, EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Finalize(IntPtr statement);

        [DllImport(LibraryName, EntryPoint = "sqlite3_bind_parameter_index", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int BindParameterIndex(IntPtr statement, byte[] name);

        [DllImport(LibraryName, EntryPoint = "sqlite3_bind_null", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int BindNull(IntPtr statement, int index);

        [DllImport(LibraryName, EntryPoint = "sqlite3_bind_int64", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int BindInt64(IntPtr statement, int index, long value);

        [DllImport(LibraryName, EntryPoint = "sqlite3_bind_text", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int BindText(
            IntPtr statement,
            int index,
            byte[] value,
            int byteCount,
            IntPtr destructor);

        [DllImport(LibraryName, EntryPoint = "sqlite3_column_int64", CallingConvention = CallingConvention.Cdecl)]
        internal static extern long ColumnInt64(IntPtr statement, int column);

        [DllImport(LibraryName, EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ColumnText(IntPtr statement, int column);

        [DllImport(LibraryName, EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ColumnBytes(IntPtr statement, int column);

        [DllImport(LibraryName, EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ErrorMessage(IntPtr database);

        [DllImport(LibraryName, EntryPoint = "sqlite3_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Free(IntPtr memory);

        internal static byte[] ToUtf8(string value)
        {
            byte[] content = Encoding.UTF8.GetBytes(value);
            var terminated = new byte[content.Length + 1];
            Buffer.BlockCopy(content, 0, terminated, 0, content.Length);
            return terminated;
        }

        internal static string ReadUtf8(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
            {
                return string.Empty;
            }

            int byteCount = 0;
            while (Marshal.ReadByte(pointer, byteCount) != 0)
            {
                byteCount++;
            }

            return ReadUtf8(pointer, byteCount);
        }

        internal static string ReadUtf8(IntPtr pointer, int byteCount)
        {
            if (pointer == IntPtr.Zero || byteCount == 0)
            {
                return string.Empty;
            }

            var bytes = new byte[byteCount];
            Marshal.Copy(pointer, bytes, 0, byteCount);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}

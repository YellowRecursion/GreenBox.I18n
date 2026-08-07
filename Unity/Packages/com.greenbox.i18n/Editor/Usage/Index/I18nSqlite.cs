#nullable enable

using System;
using System.IO;

namespace GreenBox.I18n.Usage.Index
{
    /// <summary>
    /// Minimal SQLite connection used by the usage index without a managed provider dependency.
    /// </summary>
    internal sealed class I18nSqliteConnection : IDisposable
    {
        private const int OpenReadWrite = 0x00000002;
        private const int OpenCreate = 0x00000004;
        private const int OpenFullMutex = 0x00010000;

        private IntPtr _handle;

        public I18nSqliteConnection(string databasePath)
        {
            string fullPath = Path.GetFullPath(databasePath);
            int result = I18nSqliteNative.Open(
                I18nSqliteNative.ToUtf8(fullPath),
                out _handle,
                OpenReadWrite | OpenCreate | OpenFullMutex,
                IntPtr.Zero);
            if (result != I18nSqliteNative.Ok)
            {
                string message = _handle == IntPtr.Zero
                    ? $"Could not open SQLite database '{fullPath}'."
                    : GetErrorMessage();
                Dispose();
                throw new I18nSqliteException(result, message);
            }
        }

        internal IntPtr Handle => _handle != IntPtr.Zero
            ? _handle
            : throw new ObjectDisposedException(nameof(I18nSqliteConnection));

        public void SetBusyTimeout(int milliseconds)
        {
            ThrowOnError(I18nSqliteNative.BusyTimeout(Handle, milliseconds));
        }

        public void Execute(string sql)
        {
            IntPtr errorPointer = IntPtr.Zero;
            int result = I18nSqliteNative.Execute(
                Handle,
                I18nSqliteNative.ToUtf8(sql),
                IntPtr.Zero,
                IntPtr.Zero,
                out errorPointer);
            if (result == I18nSqliteNative.Ok)
            {
                return;
            }

            string message = errorPointer != IntPtr.Zero
                ? I18nSqliteNative.ReadUtf8(errorPointer)
                : GetErrorMessage();
            if (errorPointer != IntPtr.Zero)
            {
                I18nSqliteNative.Free(errorPointer);
            }

            throw new I18nSqliteException(result, message);
        }

        public long ExecuteScalarInt64(string sql)
        {
            using I18nSqliteStatement statement = Prepare(sql);
            return statement.ExecuteScalarInt64();
        }

        public string ExecuteScalarString(string sql)
        {
            using I18nSqliteStatement statement = Prepare(sql);
            return statement.ExecuteScalarString();
        }

        public I18nSqliteStatement Prepare(string sql)
        {
            return new I18nSqliteStatement(this, sql);
        }

        public I18nSqliteTransaction BeginTransaction()
        {
            return new I18nSqliteTransaction(this, true);
        }

        public I18nSqliteTransaction BeginReadTransaction()
        {
            return new I18nSqliteTransaction(this, false);
        }

        internal void ThrowOnError(int result)
        {
            if (result != I18nSqliteNative.Ok)
            {
                throw new I18nSqliteException(result, GetErrorMessage());
            }
        }

        internal string GetErrorMessage()
        {
            return I18nSqliteNative.ReadUtf8(I18nSqliteNative.ErrorMessage(Handle));
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero)
            {
                return;
            }

            I18nSqliteNative.Close(_handle);
            _handle = IntPtr.Zero;
        }
    }

    internal sealed class I18nSqliteTransaction : IDisposable
    {
        private readonly I18nSqliteConnection _connection;
        private bool _completed;

        public I18nSqliteTransaction(I18nSqliteConnection connection, bool immediate)
        {
            _connection = connection;
            _connection.Execute(immediate ? "BEGIN IMMEDIATE;" : "BEGIN;");
        }

        public void Commit()
        {
            if (_completed)
            {
                throw new InvalidOperationException("The SQLite transaction has already completed.");
            }

            _connection.Execute("COMMIT;");
            _completed = true;
        }

        public void Dispose()
        {
            if (_completed)
            {
                return;
            }

            try
            {
                _connection.Execute("ROLLBACK;");
            }
            catch (I18nSqliteException)
            {
                // Dispose must not hide the operation that caused the transaction to unwind.
            }
            finally
            {
                _completed = true;
            }
        }
    }

    internal sealed class I18nSqliteStatement : IDisposable
    {
        private readonly I18nSqliteConnection _connection;
        private IntPtr _handle;

        public I18nSqliteStatement(I18nSqliteConnection connection, string sql)
        {
            _connection = connection;
            int result = I18nSqliteNative.Prepare(
                connection.Handle,
                I18nSqliteNative.ToUtf8(sql),
                -1,
                out _handle,
                IntPtr.Zero);
            if (result != I18nSqliteNative.Ok)
            {
                string message = connection.GetErrorMessage();
                if (_handle != IntPtr.Zero)
                {
                    I18nSqliteNative.Finalize(_handle);
                    _handle = IntPtr.Zero;
                }

                throw new I18nSqliteException(result, message);
            }
        }

        public I18nSqliteStatement Bind(string name, string? value)
        {
            int index = GetParameterIndex(name);
            int result = value == null
                ? I18nSqliteNative.BindNull(_handle, index)
                : BindText(index, value);
            _connection.ThrowOnError(result);
            return this;
        }

        public I18nSqliteStatement Bind(string name, long value)
        {
            _connection.ThrowOnError(I18nSqliteNative.BindInt64(
                _handle,
                GetParameterIndex(name),
                value));
            return this;
        }

        public I18nSqliteStatement BindNull(string name)
        {
            _connection.ThrowOnError(I18nSqliteNative.BindNull(
                _handle,
                GetParameterIndex(name)));
            return this;
        }

        public void ExecuteNonQuery()
        {
            int result = I18nSqliteNative.Step(_handle);
            if (result != I18nSqliteNative.Done)
            {
                throw new I18nSqliteException(result, _connection.GetErrorMessage());
            }

            Reset();
        }

        public long ExecuteScalarInt64()
        {
            int result = I18nSqliteNative.Step(_handle);
            if (result != I18nSqliteNative.Row)
            {
                throw new I18nSqliteException(result, _connection.GetErrorMessage());
            }

            long value = I18nSqliteNative.ColumnInt64(_handle, 0);
            Reset();
            return value;
        }

        public string ExecuteScalarString()
        {
            int result = I18nSqliteNative.Step(_handle);
            if (result != I18nSqliteNative.Row)
            {
                throw new I18nSqliteException(result, _connection.GetErrorMessage());
            }

            IntPtr textPointer = I18nSqliteNative.ColumnText(_handle, 0);
            int byteCount = I18nSqliteNative.ColumnBytes(_handle, 0);
            string value = I18nSqliteNative.ReadUtf8(textPointer, byteCount);
            Reset();
            return value;
        }

        public bool Read()
        {
            int result = I18nSqliteNative.Step(_handle);
            if (result == I18nSqliteNative.Row)
            {
                return true;
            }

            if (result == I18nSqliteNative.Done)
            {
                Reset();
                return false;
            }

            throw new I18nSqliteException(result, _connection.GetErrorMessage());
        }

        public long GetInt64(int column)
        {
            return I18nSqliteNative.ColumnInt64(_handle, column);
        }

        public string GetString(int column)
        {
            IntPtr textPointer = I18nSqliteNative.ColumnText(_handle, column);
            int byteCount = I18nSqliteNative.ColumnBytes(_handle, column);
            return I18nSqliteNative.ReadUtf8(textPointer, byteCount);
        }

        private int BindText(int index, string value)
        {
            byte[] bytes = I18nSqliteNative.ToUtf8(value);
            return I18nSqliteNative.BindText(
                _handle,
                index,
                bytes,
                bytes.Length - 1,
                new IntPtr(-1));
        }

        private int GetParameterIndex(string name)
        {
            int index = I18nSqliteNative.BindParameterIndex(
                _handle,
                I18nSqliteNative.ToUtf8(name));
            if (index == 0)
            {
                throw new ArgumentException($"SQLite parameter '{name}' does not exist.", nameof(name));
            }

            return index;
        }

        private void Reset()
        {
            _connection.ThrowOnError(I18nSqliteNative.Reset(_handle));
            _connection.ThrowOnError(I18nSqliteNative.ClearBindings(_handle));
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero)
            {
                return;
            }

            I18nSqliteNative.Finalize(_handle);
            _handle = IntPtr.Zero;
        }
    }

    internal sealed class I18nSqliteException : Exception
    {
        public I18nSqliteException(int resultCode, string message)
            : base($"SQLite error {resultCode}: {message}")
        {
            ResultCode = resultCode;
        }

        public int ResultCode { get; }
    }

}

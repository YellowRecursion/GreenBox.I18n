using System.Text;

namespace GreenBox.I18n.Cli;

internal static class CatalogFileWriter
{
    public static CliError? WriteAtomically(FileInfo catalogFile, string content)
    {
        string directory = catalogFile.DirectoryName!;
        string temporaryPath = Path.Combine(
            directory,
            $".{catalogFile.Name}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(true);
            }

            File.Replace(temporaryPath, catalogFile.FullName, null);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new CliError
            {
                Code = CliDiagnosticCodes.FileWriteFailed,
                Message = exception.Message,
            };
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The original catalog is intact; a stale temporary file can be removed later.
            }
        }
    }
}

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace MechwarriorVRLauncher.Services
{
    public class ZipExtractionService
    {
        public event EventHandler<string>? ProgressChanged;

        public async Task<bool> ExtractZipAsync(string zipFilePath, string destinationDirectory)
        {
            if (!File.Exists(zipFilePath))
            {
                OnProgressChanged($"Error: Zip file not found: {zipFilePath}");
                return false;
            }

            try
            {
                OnProgressChanged($"Extracting {Path.GetFileName(zipFilePath)} to {destinationDirectory}...");

                // Ensure destination directory exists
                Directory.CreateDirectory(destinationDirectory);

                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(zipFilePath);
                    foreach (var entry in archive.Entries)
                    {
                        // Get the full path for the destination file
                        var destinationPath = Path.Combine(destinationDirectory, entry.FullName);

                        // Create directory if it doesn't exist
                        var directoryPath = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }

                        // Extract file if it's not a directory
                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            entry.ExtractToFile(destinationPath, overwrite: true);
                        }
                    }
                });

                OnProgressChanged($"Successfully extracted {Path.GetFileName(zipFilePath)}");
                return true;
            }
            catch (Exception ex)
            {
                OnProgressChanged($"Error extracting {Path.GetFileName(zipFilePath)}: {ex.Message}");
                return false;
            }
        }

        public async Task<int> ExtractMultipleZipsAsync(string[] zipFiles, string destinationDirectory)
        {
            int successCount = 0;

            foreach (var zipFile in zipFiles)
            {
                var success = await ExtractZipAsync(zipFile, destinationDirectory);
                if (success)
                {
                    successCount++;
                }
            }

            return successCount;
        }

        protected virtual void OnProgressChanged(string message)
        {
            ProgressChanged?.Invoke(this, message);
        }
    }
}

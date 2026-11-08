using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DiskManager.Models;

namespace DiskManager.Services
{
    public class DuplicateFileService
    {
        public event EventHandler<ProgressEventArgs>? ProgressChanged;

        public async Task<List<DuplicateFileGroup>> FindDuplicatesAsync(string path, IProgress<int>? progress = null)
        {
            return await Task.Run(() =>
            {
                var duplicates = new Dictionary<string, DuplicateFileGroup>();
                var filesBySize = new Dictionary<long, List<string>>();

                var allFiles = GetAllFiles(path);
                var totalFiles = allFiles.Count;
                var processedFiles = 0;

                foreach (var file in allFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (!filesBySize.ContainsKey(fileInfo.Length))
                        {
                            filesBySize[fileInfo.Length] = new List<string>();
                        }
                        filesBySize[fileInfo.Length].Add(file);
                    }
                    catch (Exception) { }

                    processedFiles++;
                    progress?.Report((int)((double)processedFiles / totalFiles * 25));
                }

                var sizeGroupsWithMultipleFiles = filesBySize.Where(kv => kv.Value.Count > 1).ToList();
                processedFiles = 0;

                foreach (var sizeGroup in sizeGroupsWithMultipleFiles)
                {
                    foreach (var file in sizeGroup.Value)
                    {
                        try
                        {
                            var hash = ComputeFileHash(file);
                            if (!duplicates.ContainsKey(hash))
                            {
                                duplicates[hash] = new DuplicateFileGroup
                                {
                                    FileHash = hash,
                                    FileSize = sizeGroup.Key
                                };
                            }

                            var fileInfo = new FileInfo(file);
                            duplicates[hash].Files.Add(new FileItem
                            {
                                FullPath = file,
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTime,
                                IsDirectory = false
                            });
                        }
                        catch (Exception) { }
                    }

                    processedFiles++;
                    progress?.Report(25 + (int)((double)processedFiles / sizeGroupsWithMultipleFiles.Count * 75));
                }

                return duplicates.Values.Where(g => g.Files.Count > 1).OrderByDescending(g => g.TotalWastedSpace).ToList();
            });
        }

        private List<string> GetAllFiles(string path)
        {
            var files = new List<string>();
            try
            {
                files.AddRange(Directory.GetFiles(path));
                foreach (var dir in Directory.GetDirectories(path))
                {
                    try
                    {
                        files.AddRange(GetAllFiles(dir));
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            return files;
        }

        private string ComputeFileHash(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}

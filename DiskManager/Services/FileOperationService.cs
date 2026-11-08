using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiskManager.Models;

namespace DiskManager.Services
{
    public class FileOperationService
    {
        private readonly CleanupService _cleanupService;
        private readonly LogService _logService;

        public FileOperationService(CleanupService cleanupService, LogService logService)
        {
            _cleanupService = cleanupService;
            _logService = logService;
        }

        public async Task<bool> CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_cleanupService.IsSystemProtectedPath(sourcePath) || 
                        _cleanupService.IsSystemProtectedPath(destinationPath))
                    {
                        _logService.Log("复制文件", $"拒绝访问受保护路径: {sourcePath} -> {destinationPath}", false);
                        return false;
                    }

                    File.Copy(sourcePath, destinationPath, overwrite);
                    _logService.Log("复制文件", $"{sourcePath} -> {destinationPath}", true);
                    return true;
                }
                catch (Exception ex)
                {
                    _logService.Log("复制文件", $"{sourcePath} -> {destinationPath}", false, ex.Message);
                    return false;
                }
            });
        }

        public async Task<bool> MoveFileAsync(string sourcePath, string destinationPath, bool overwrite = false)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_cleanupService.IsSystemProtectedPath(sourcePath) || 
                        _cleanupService.IsSystemProtectedPath(destinationPath))
                    {
                        _logService.Log("移动文件", $"拒绝访问受保护路径: {sourcePath} -> {destinationPath}", false);
                        return false;
                    }

                    if (overwrite && File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath);
                    }

                    File.Move(sourcePath, destinationPath);
                    _logService.Log("移动文件", $"{sourcePath} -> {destinationPath}", true);
                    return true;
                }
                catch (Exception ex)
                {
                    _logService.Log("移动文件", $"{sourcePath} -> {destinationPath}", false, ex.Message);
                    return false;
                }
            });
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var result = _cleanupService.DeleteFile(filePath, false);
                    if (result)
                    {
                        _logService.Log("删除文件", filePath, true);
                    }
                    else
                    {
                        _logService.Log("删除文件", filePath, false, "删除失败或拒绝访问");
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    _logService.Log("删除文件", filePath, false, ex.Message);
                    return false;
                }
            });
        }

        public async Task<bool> BatchCopyAsync(List<string> sourcePaths, string destinationFolder, IProgress<int>? progress = null)
        {
            var success = true;
            var processed = 0;

            foreach (var sourcePath in sourcePaths)
            {
                var fileName = Path.GetFileName(sourcePath);
                var destinationPath = Path.Combine(destinationFolder, fileName);
                var result = await CopyFileAsync(sourcePath, destinationPath, true);
                if (!result)
                {
                    success = false;
                }
                processed++;
                progress?.Report((int)((double)processed / sourcePaths.Count * 100));
            }

            return success;
        }

        public async Task<bool> BatchMoveAsync(List<string> sourcePaths, string destinationFolder, IProgress<int>? progress = null)
        {
            var success = true;
            var processed = 0;

            foreach (var sourcePath in sourcePaths)
            {
                var fileName = Path.GetFileName(sourcePath);
                var destinationPath = Path.Combine(destinationFolder, fileName);
                var result = await MoveFileAsync(sourcePath, destinationPath, true);
                if (!result)
                {
                    success = false;
                }
                processed++;
                progress?.Report((int)((double)processed / sourcePaths.Count * 100));
            }

            return success;
        }

        public async Task<bool> BatchDeleteAsync(List<string> filePaths, IProgress<int>? progress = null)
        {
            var success = true;
            var processed = 0;

            foreach (var filePath in filePaths)
            {
                var result = await DeleteFileAsync(filePath);
                if (!result)
                {
                    success = false;
                }
                processed++;
                progress?.Report((int)((double)processed / filePaths.Count * 100));
            }

            return success;
        }

        public bool CreateDirectory(string path)
        {
            try
            {
                if (_cleanupService.IsSystemProtectedPath(path))
                {
                    _logService.Log("创建目录", $"拒绝访问受保护路径: {path}", false);
                    return false;
                }

                Directory.CreateDirectory(path);
                _logService.Log("创建目录", path, true);
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log("创建目录", path, false, ex.Message);
                return false;
            }
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public long GetDirectorySize(string path)
        {
            long size = 0;
            try
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    try
                    {
                        size += new FileInfo(file).Length;
                    }
                    catch (Exception) { }
                }

                foreach (var dir in Directory.GetDirectories(path))
                {
                    try
                    {
                        size += GetDirectorySize(dir);
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
            return size;
        }
    }
}

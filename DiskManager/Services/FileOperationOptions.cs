using DiskManager.Models;

namespace DiskManager.Services;

public sealed class FileOperationOptions
{
    public ConflictPolicy Conflict { get; init; } = ConflictPolicy.Rename;
    public DeleteMode DeleteMode { get; init; } = DeleteMode.RecycleBin;
}
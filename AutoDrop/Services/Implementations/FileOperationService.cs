using AutoDrop.Models;
using AutoDrop.Services.Interfaces;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of file operation service for moving files and folders.
/// </summary>
public sealed class FileOperationService : IFileOperationService
{
    /// <inheritdoc />
    public async Task<MoveOperation> MoveAsync(string sourcePath, string destinationFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);

        if (!Exists(sourcePath))
        {
            throw new FileNotFoundException("Source file or folder not found.", sourcePath);
        }

        if (!Directory.Exists(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder);
        }

        var isDirectory = Directory.Exists(sourcePath);
        var itemName = Path.GetFileName(sourcePath);
        var destinationPath = GetUniqueFilePath(destinationFolder, itemName);

        await Task.Run(() =>
        {
            if (isDirectory)
            {
                Directory.Move(sourcePath, destinationPath);
            }
            else
            {
                File.Move(sourcePath, destinationPath);
            }
        });

        return new MoveOperation
        {
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            ItemName = itemName,
            IsDirectory = isDirectory
        };
    }

    /// <inheritdoc />
    public async Task<bool> UndoMoveAsync(MoveOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!operation.CanUndo)
        {
            return false;
        }

        if (!Exists(operation.DestinationPath))
        {
            return false;
        }

        // Check if source location is available
        var sourceDirectory = Path.GetDirectoryName(operation.SourcePath);
        if (!string.IsNullOrEmpty(sourceDirectory) && !Directory.Exists(sourceDirectory))
        {
            Directory.CreateDirectory(sourceDirectory);
        }

        var targetPath = operation.SourcePath;
        
        // If original source path is now occupied, generate unique name
        if (Exists(targetPath))
        {
            var directory = Path.GetDirectoryName(targetPath) ?? string.Empty;
            var fileName = Path.GetFileName(targetPath);
            targetPath = GetUniqueFilePath(directory, fileName);
        }

        await Task.Run(() =>
        {
            if (operation.IsDirectory)
            {
                Directory.Move(operation.DestinationPath, targetPath);
            }
            else
            {
                File.Move(operation.DestinationPath, targetPath);
            }
        });

        operation.CanUndo = false;
        return true;
    }

    /// <inheritdoc />
    public bool Exists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    /// <inheritdoc />
    public string GetUniqueFilePath(string destinationFolder, string fileName)
    {
        var destinationPath = Path.Combine(destinationFolder, fileName);

        if (!Exists(destinationPath))
        {
            return destinationPath;
        }

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var counter = 1;

        do
        {
            var newFileName = $"{nameWithoutExtension} ({counter}){extension}";
            destinationPath = Path.Combine(destinationFolder, newFileName);
            counter++;
        }
        while (Exists(destinationPath));

        return destinationPath;
    }
}

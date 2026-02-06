using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of file operation service for moving files and folders.
/// </summary>
public sealed class FileOperationService : IFileOperationService
{
    private readonly ILogger<FileOperationService> _logger;

    public FileOperationService(ILogger<FileOperationService> logger)
    {
        _logger = logger;
        _logger.LogDebug("FileOperationService initialized");
    }

    /// <inheritdoc />
    public async Task<MoveOperation> MoveAsync(string sourcePath, string destinationFolder, string? suggestedFileName = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);

        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Moving: {Source} -> {Destination}", sourcePath, destinationFolder);

        if (!Exists(sourcePath))
        {
            _logger.LogError("Source not found: {Source}", sourcePath);
            throw new FileNotFoundException("Source file or folder not found.", sourcePath);
        }

        if (!Directory.Exists(destinationFolder))
        {
            _logger.LogDebug("Creating destination folder: {Destination}", destinationFolder);
            Directory.CreateDirectory(destinationFolder);
        }

        var isDirectory = Directory.Exists(sourcePath);
        var itemName = Path.GetFileName(sourcePath);

        // Apply AI-suggested filename if provided (files only, keep original extension)
        if (!isDirectory && !string.IsNullOrWhiteSpace(suggestedFileName))
        {
            var originalExtension = Path.GetExtension(sourcePath);
            itemName = suggestedFileName + originalExtension;
            _logger.LogInformation("Smart rename: {Original} -> {NewName}", Path.GetFileName(sourcePath), itemName);
        }

        var destinationPath = GetUniqueFilePath(destinationFolder, itemName);

        _logger.LogDebug("Move details: IsDirectory={IsDirectory}, ItemName={ItemName}, FinalPath={FinalPath}", 
            isDirectory, itemName, destinationPath);

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (isDirectory)
            {
                Directory.Move(sourcePath, destinationPath);
            }
            else
            {
                File.Move(sourcePath, destinationPath);
            }
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Move completed: {ItemName} -> {Destination}", itemName, destinationPath);

        return new MoveOperation
        {
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            ItemName = itemName,
            IsDirectory = isDirectory
        };
    }

    /// <inheritdoc />
    public async Task<bool> UndoMoveAsync(MoveOperation operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Undoing move: {ItemName}", operation.ItemName);

        if (!operation.CanUndo)
        {
            _logger.LogWarning("Undo not available for: {ItemName}", operation.ItemName);
            return false;
        }

        if (!Exists(operation.DestinationPath))
        {
            _logger.LogWarning("Destination no longer exists for undo: {Path}", operation.DestinationPath);
            return false;
        }

        // Check if source location is available
        var sourceDirectory = Path.GetDirectoryName(operation.SourcePath);
        if (!string.IsNullOrEmpty(sourceDirectory) && !Directory.Exists(sourceDirectory))
        {
            _logger.LogDebug("Recreating source directory: {Directory}", sourceDirectory);
            Directory.CreateDirectory(sourceDirectory);
        }

        var targetPath = operation.SourcePath;
        
        // If original source path is now occupied, generate unique name
        if (Exists(targetPath))
        {
            var directory = Path.GetDirectoryName(targetPath) ?? string.Empty;
            var fileName = Path.GetFileName(targetPath);
            targetPath = GetUniqueFilePath(directory, fileName);
            _logger.LogDebug("Original path occupied, using: {NewPath}", targetPath);
        }

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.IsDirectory)
            {
                Directory.Move(operation.DestinationPath, targetPath);
            }
            else
            {
                File.Move(operation.DestinationPath, targetPath);
            }
        }, cancellationToken).ConfigureAwait(false);

        operation.CanUndo = false;
        _logger.LogInformation("Undo completed: {ItemName} restored to {Path}", operation.ItemName, targetPath);
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

        _logger.LogDebug("Generated unique path: {Path}", destinationPath);
        return destinationPath;
    }
}

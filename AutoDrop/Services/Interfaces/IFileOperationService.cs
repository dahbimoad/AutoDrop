using AutoDrop.Models;

namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Service responsible for file and folder move operations.
/// </summary>
public interface IFileOperationService
{
    /// <summary>
    /// Moves a file or folder to the specified destination.
    /// </summary>
    /// <param name="sourcePath">Source file or folder path.</param>
    /// <param name="destinationFolder">Destination folder path.</param>
    /// <returns>The completed move operation details.</returns>
    Task<MoveOperation> MoveAsync(string sourcePath, string destinationFolder);

    /// <summary>
    /// Undoes a previous move operation.
    /// </summary>
    /// <param name="operation">The operation to undo.</param>
    /// <returns>True if undo was successful.</returns>
    Task<bool> UndoMoveAsync(MoveOperation operation);

    /// <summary>
    /// Checks if a file or folder exists.
    /// </summary>
    /// <param name="path">Path to check.</param>
    /// <returns>True if exists.</returns>
    bool Exists(string path);

    /// <summary>
    /// Generates a unique filename if the target already exists.
    /// </summary>
    /// <param name="destinationFolder">Destination folder.</param>
    /// <param name="fileName">Original file name.</param>
    /// <returns>Unique file path.</returns>
    string GetUniqueFilePath(string destinationFolder, string fileName);
}

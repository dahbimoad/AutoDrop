namespace AutoDrop.Tests.Fixtures;

/// <summary>
/// Provides temporary file and folder management for integration tests.
/// Creates isolated test directories that are automatically cleaned up.
/// </summary>
public sealed class TestFileFixture : IDisposable
{
    private readonly List<string> _createdPaths = [];
    private bool _disposed;

    /// <summary>
    /// Base directory for all test files in this fixture instance.
    /// </summary>
    public string TestDirectory { get; }

    public TestFileFixture()
    {
        // Create a unique test directory for each fixture instance
        TestDirectory = Path.Combine(Path.GetTempPath(), "AutoDrop_Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TestDirectory);
        _createdPaths.Add(TestDirectory);
    }

    /// <summary>
    /// Creates a temporary file with the specified name and content.
    /// </summary>
    /// <param name="fileName">Name of the file to create.</param>
    /// <param name="content">Optional content to write to the file.</param>
    /// <returns>Full path to the created file.</returns>
    public string CreateFile(string fileName, string? content = null)
    {
        var filePath = Path.Combine(TestDirectory, fileName);
        var directory = Path.GetDirectoryName(filePath);
        
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, content ?? $"Test content for {fileName}");
        _createdPaths.Add(filePath);
        return filePath;
    }

    /// <summary>
    /// Creates a file with the specified size (for testing large file handling).
    /// </summary>
    /// <param name="fileName">Name of the file to create.</param>
    /// <param name="sizeInBytes">Size of the file in bytes.</param>
    /// <returns>Full path to the created file.</returns>
    public string CreateFileWithSize(string fileName, long sizeInBytes)
    {
        var filePath = Path.Combine(TestDirectory, fileName);
        
        using var fs = File.Create(filePath);
        
        // Write in chunks to avoid memory issues with large files
        const int bufferSize = 8192;
        var buffer = new byte[bufferSize];
        var random = new Random(42); // Deterministic for reproducibility
        
        var remaining = sizeInBytes;
        while (remaining > 0)
        {
            var toWrite = (int)Math.Min(bufferSize, remaining);
            random.NextBytes(buffer);
            fs.Write(buffer, 0, toWrite);
            remaining -= toWrite;
        }
        
        _createdPaths.Add(filePath);
        return filePath;
    }

    /// <summary>
    /// Creates a temporary directory.
    /// </summary>
    /// <param name="directoryName">Name of the directory to create.</param>
    /// <returns>Full path to the created directory.</returns>
    public string CreateDirectory(string directoryName)
    {
        var directoryPath = Path.Combine(TestDirectory, directoryName);
        Directory.CreateDirectory(directoryPath);
        _createdPaths.Add(directoryPath);
        return directoryPath;
    }

    /// <summary>
    /// Creates a directory structure with files for comprehensive testing.
    /// </summary>
    /// <param name="directoryName">Root directory name.</param>
    /// <param name="fileNames">File names to create inside the directory.</param>
    /// <returns>Full path to the created directory.</returns>
    public string CreateDirectoryWithFiles(string directoryName, params string[] fileNames)
    {
        var directoryPath = CreateDirectory(directoryName);
        
        foreach (var fileName in fileNames)
        {
            var filePath = Path.Combine(directoryPath, fileName);
            File.WriteAllText(filePath, $"Content of {fileName}");
        }
        
        return directoryPath;
    }

    /// <summary>
    /// Gets a path within the test directory (does not create the file/folder).
    /// </summary>
    /// <param name="relativePath">Relative path within test directory.</param>
    /// <returns>Full path.</returns>
    public string GetPath(string relativePath)
    {
        return Path.Combine(TestDirectory, relativePath);
    }

    /// <summary>
    /// Creates a subdirectory within the test directory.
    /// </summary>
    /// <param name="name">Subdirectory name.</param>
    /// <returns>Full path to the subdirectory.</returns>
    public string CreateSubDirectory(string name)
    {
        var path = Path.Combine(TestDirectory, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Clean up in reverse order (files before directories)
        foreach (var path in _createdPaths.AsEnumerable().Reverse())
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best effort cleanup - ignore failures
            }
        }
    }
}

/// <summary>
/// Collection fixture for sharing test infrastructure across test classes.
/// </summary>
[CollectionDefinition("FileSystem")]
public class FileSystemCollection : ICollectionFixture<TestFileFixture>
{
}

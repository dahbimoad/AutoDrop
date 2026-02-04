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
    /// Creates a minimal valid image file for testing.
    /// </summary>
    /// <param name="fileName">Name of the image file (e.g., "test.jpg").</param>
    /// <returns>Full path to the created image file.</returns>
    public string CreateImageFile(string fileName)
    {
        var filePath = Path.Combine(TestDirectory, fileName);
        var directory = Path.GetDirectoryName(filePath);
        
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Create a minimal 1x1 pixel JPEG (smallest valid JPEG)
        // This is a complete valid JPEG with SOI, APP0, DQT, SOF0, DHT, SOS, image data, EOI markers
        byte[] minimalJpeg =
        [
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
            0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43,
            0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08, 0x07, 0x07, 0x07, 0x09,
            0x09, 0x08, 0x0A, 0x0C, 0x14, 0x0D, 0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12,
            0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D, 0x1A, 0x1C, 0x1C, 0x20,
            0x24, 0x2E, 0x27, 0x20, 0x22, 0x2C, 0x23, 0x1C, 0x1C, 0x28, 0x37, 0x29,
            0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27, 0x39, 0x3D, 0x38, 0x32,
            0x3C, 0x2E, 0x33, 0x34, 0x32, 0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01,
            0x00, 0x01, 0x01, 0x01, 0x11, 0x00, 0xFF, 0xC4, 0x00, 0x1F, 0x00, 0x00,
            0x01, 0x05, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x09, 0x0A, 0x0B, 0xFF, 0xC4, 0x00, 0xB5, 0x10, 0x00, 0x02, 0x01, 0x03,
            0x03, 0x02, 0x04, 0x03, 0x05, 0x05, 0x04, 0x04, 0x00, 0x00, 0x01, 0x7D,
            0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06,
            0x13, 0x51, 0x61, 0x07, 0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08,
            0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52, 0xD1, 0xF0, 0x24, 0x33, 0x62, 0x72,
            0x82, 0x09, 0x0A, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x25, 0x26, 0x27, 0x28,
            0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45,
            0x46, 0x47, 0x48, 0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
            0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x73, 0x74, 0x75,
            0x76, 0x77, 0x78, 0x79, 0x7A, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
            0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3,
            0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6,
            0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9,
            0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xE1, 0xE2,
            0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF1, 0xF2, 0xF3, 0xF4,
            0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA, 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01,
            0x00, 0x00, 0x3F, 0x00, 0xFB, 0xD5, 0xDB, 0x00, 0x31, 0xC4, 0x1F, 0xFF,
            0xD9
        ];
        
        File.WriteAllBytes(filePath, minimalJpeg);
        _createdPaths.Add(filePath);
        return filePath;
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

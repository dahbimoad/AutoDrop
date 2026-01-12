using System.Security.Cryptography;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of duplicate detection service using SHA256 hash comparison.
/// </summary>
public sealed class DuplicateDetectionService : IDuplicateDetectionService, IDisposable
{
    private readonly ILogger<DuplicateDetectionService> _logger;
    private readonly SHA256 _sha256;
    private bool _disposed;

    /// <summary>
    /// Default maximum file size for hash comparison (100 MB).
    /// </summary>
    private const long DefaultMaxHashFileSize = 100 * 1024 * 1024;

    /// <summary>
    /// Buffer size for file reading (64 KB).
    /// </summary>
    private const int BufferSize = 64 * 1024;

    /// <inheritdoc />
    public long MaxHashFileSizeBytes { get; } = DefaultMaxHashFileSize;

    /// <inheritdoc />
    public bool IsEnabled { get; set; } = true;

    public DuplicateDetectionService(ILogger<DuplicateDetectionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sha256 = SHA256.Create();
        _logger.LogDebug("DuplicateDetectionService initialized");
    }

    /// <inheritdoc />
    public async Task<DuplicateCheckResult> CheckForDuplicateAsync(
        string sourceFilePath,
        string destinationFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);

        if (!IsEnabled)
        {
            _logger.LogDebug("Duplicate detection is disabled, skipping check");
            return DuplicateCheckResult.NoDuplicate();
        }

        try
        {
            // Check if source file exists
            if (!File.Exists(sourceFilePath))
            {
                return DuplicateCheckResult.Error($"Source file not found: {sourceFilePath}");
            }

            // Check if destination file exists
            if (!File.Exists(destinationFilePath))
            {
                _logger.LogDebug("Destination file does not exist: {Path}", destinationFilePath);
                return DuplicateCheckResult.NoDuplicate();
            }

            var sourceInfo = new FileInfo(sourceFilePath);
            var destInfo = new FileInfo(destinationFilePath);

            var sourceComparisonInfo = new FileComparisonInfo
            {
                FilePath = sourceFilePath,
                Size = sourceInfo.Length,
                LastModified = sourceInfo.LastWriteTimeUtc
            };

            var destComparisonInfo = new FileComparisonInfo
            {
                FilePath = destinationFilePath,
                Size = destInfo.Length,
                LastModified = destInfo.LastWriteTimeUtc
            };

            // Step 1: Compare file sizes (fast check)
            if (sourceInfo.Length != destInfo.Length)
            {
                _logger.LogDebug("Files have different sizes: {SourceSize} vs {DestSize}", 
                    sourceInfo.Length, destInfo.Length);
                
                return new DuplicateCheckResult
                {
                    IsDuplicate = false,
                    IsExactMatch = false,
                    ComparisonMethod = DuplicateComparisonMethod.SizeOnly,
                    Source = sourceComparisonInfo,
                    Destination = destComparisonInfo
                };
            }

            // Step 2: For large files, use size + date comparison
            if (sourceInfo.Length > MaxHashFileSizeBytes)
            {
                _logger.LogDebug("File exceeds hash size limit ({Size} > {Max}), using size/date comparison",
                    sourceInfo.Length, MaxHashFileSizeBytes);

                var isDuplicateByDate = sourceInfo.Length == destInfo.Length &&
                                        Math.Abs((sourceInfo.LastWriteTimeUtc - destInfo.LastWriteTimeUtc).TotalSeconds) < 2;

                return new DuplicateCheckResult
                {
                    IsDuplicate = isDuplicateByDate,
                    IsExactMatch = isDuplicateByDate,
                    ComparisonMethod = DuplicateComparisonMethod.SizeAndDate,
                    Source = sourceComparisonInfo,
                    Destination = destComparisonInfo
                };
            }

            // Step 3: Compute and compare hashes for exact match
            _logger.LogDebug("Computing hashes for duplicate detection");

            var sourceHash = await ComputeFileHashAsync(sourceFilePath, cancellationToken).ConfigureAwait(false);
            var destHash = await ComputeFileHashAsync(destinationFilePath, cancellationToken).ConfigureAwait(false);

            var isExactMatch = string.Equals(sourceHash, destHash, StringComparison.OrdinalIgnoreCase);

            _logger.LogDebug("Hash comparison result: {IsMatch} (source: {SourceHash}, dest: {DestHash})",
                isExactMatch, sourceHash[..8], destHash[..8]);

            return new DuplicateCheckResult
            {
                IsDuplicate = isExactMatch,
                IsExactMatch = isExactMatch,
                ComparisonMethod = DuplicateComparisonMethod.Hash,
                Source = sourceComparisonInfo with { Hash = sourceHash },
                Destination = destComparisonInfo with { Hash = destHash }
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Duplicate check cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during duplicate detection for {Source} -> {Dest}", 
                sourceFilePath, destinationFilePath);
            return DuplicateCheckResult.Error(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var hashBytes = await _sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Releases resources used by the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _sha256.Dispose();
        _disposed = true;
        _logger.LogDebug("DuplicateDetectionService disposed");
    }
}

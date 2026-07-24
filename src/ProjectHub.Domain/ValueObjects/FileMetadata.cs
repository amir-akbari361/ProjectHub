using ProjectHub.Domain.Common;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Domain.ValueObjects;

public sealed class FileMetadata : ValueObject
{
    private const int MaxFileNameLength = 260;
    private const long MaxSizeInBytes = 25 * 1024 * 1024; // 25 MB

    private FileMetadata(string fileName, string contentType, long sizeInBytes)
    {
        FileName = fileName;
        ContentType = contentType;
        SizeInBytes = sizeInBytes;
    }

    public string FileName { get; }

    public string ContentType { get; }

    public long SizeInBytes { get; }

    public static FileMetadata Create(string fileName, string contentType, long sizeInBytes)
    {
        var normalizedName = Guard.NotNullOrWhiteSpace(fileName, nameof(fileName)).Trim();
        var normalizedContentType = Guard.NotNullOrWhiteSpace(contentType, nameof(contentType)).Trim();

        if (normalizedName.Length > MaxFileNameLength)
        {
            throw new DomainException($"File name cannot exceed {MaxFileNameLength} characters.");
        }

        if (sizeInBytes <= 0)
        {
            throw new DomainException("File size must be greater than zero.");
        }

        if (sizeInBytes > MaxSizeInBytes)
        {
            throw new DomainException($"File size cannot exceed {MaxSizeInBytes} bytes.");
        }

        return new FileMetadata(normalizedName, normalizedContentType, sizeInBytes);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FileName;
        yield return ContentType;
        yield return SizeInBytes;
    }
}

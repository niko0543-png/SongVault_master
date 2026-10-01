namespace SongVault.Api.Contracts.Files;

public sealed record FilePolicyResponse(IReadOnlyList<string> AllowedExtensions, long MaxSizeBytes);
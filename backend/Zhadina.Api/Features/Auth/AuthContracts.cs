namespace Zhadina.Api.Features.Auth;

// Nullable so a missing JSON field reaches our validation instead of failing binding opaquely.
public record CredentialsRequest(string? Email, string? Password);

public record UserResponse(Guid Id, string Email);

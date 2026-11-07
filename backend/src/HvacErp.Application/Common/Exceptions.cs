namespace HvacErp.Application.Common;

/// <summary>Requested entity does not exist. Maps to HTTP 404 at the Api layer.</summary>
public class NotFoundException(string entityName, object key)
    : Exception($"{entityName} '{key}' was not found.");

/// <summary>Request violates a business rule. Maps to HTTP 400 at the Api layer.</summary>
public class BusinessRuleException(string message) : Exception(message);

/// <summary>
/// Optimistic-concurrency clash — the record changed since the caller last read it (two users
/// editing the same row). Maps to HTTP 409 at the Api layer.
/// </summary>
public class ConflictException(string message) : Exception(message);

/// <summary>The caller is authenticated but their role may not perform this operation. Maps to HTTP 403.</summary>
public class ForbiddenException(string message) : Exception(message);

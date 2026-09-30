using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>Malformed / invalid request → HTTP 400.</summary>
public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}

/// <summary>
/// Resource not found, or found but not owned by the caller → HTTP 404.
/// (Deliberately does not distinguish the two, for tenant isolation.)
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Valid request but the resource is in the wrong state for the action → HTTP 409.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>Business-rule violation (over-refund, card declined, hold not renewable) → HTTP 422.</summary>
public class UnprocessableEntityException : Exception
{
    public UnprocessableEntityException(string message) : base(message) { }
}

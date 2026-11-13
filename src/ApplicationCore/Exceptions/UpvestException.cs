using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Raised when a call to Upvest fails in a way the application cannot proceed through. The message
/// is safe to surface and never contains personal data or secrets.
/// </summary>
public class UpvestException : Exception
{
    public UpvestException(string message) : base(message) { }
    public UpvestException(string message, Exception inner) : base(message, inner) { }
}

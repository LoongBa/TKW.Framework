using System;

namespace TKW.Framework.Domain.Exceptions;

public class DomainException(string message, Exception innerException) : Exception(message, innerException)
{
    public bool IsAuthenticationError { get; init; }
    public bool IsAuthorizationError { get; init; }
    public string ErrorCode { get; set; } = string.Empty;
}
using System;

namespace TKW.Framework.Domain.Exceptions;

public class DomainException(string message, Exception? innerException = null) : Exception(message, innerException)
{
    public string ErrorCode { get; set; } = string.Empty;
}
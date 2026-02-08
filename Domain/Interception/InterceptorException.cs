using System;

namespace TKW.Framework.Domain.Interception;

/// <inheritdoc />
public class InterceptorException(string message, Exception? innerException = null) : Exception(message, innerException);
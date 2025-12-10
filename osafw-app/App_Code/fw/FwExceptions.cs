// FW Exceptions
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;

// standard exceptions used by framework
[Serializable]
public class AuthException : ApplicationException
{
    public AuthException() : base("Access denied") { }
    public AuthException(string message) : base(message) { }
}

[Serializable]
public class UserException(string message) : ApplicationException(message)
{
}

[Serializable]
public class ValidationException : UserException
{
    // specificially for validation forms
    public ValidationException() : base("Please review and update your input") { }
}

[Serializable]
public class NotFoundException : UserException
{
    public NotFoundException() : base("Not Found") { }
    public NotFoundException(string message) : base(message) { }
}

[Serializable]
public class RedirectException : Exception { }

// standard exceptions with predefined messages
[Serializable]
public class FwConfigUndefinedModelException : ApplicationException
{
    public FwConfigUndefinedModelException() : base("'model' is not defined in controller's config.json") { }
}

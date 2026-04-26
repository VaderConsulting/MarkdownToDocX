using System;

namespace MDConverter;

/// <summary>
/// Thrown for critical conversion failures (template not found, stream not writable, invalid options).
/// </summary>
public sealed class MarkdownToDocxException : InvalidOperationException
{
    /// <summary>Initializes with a message.</summary>
    public MarkdownToDocxException(string Message)
        : base(Message)
    {
    }

    /// <summary>Initializes with a message and inner exception.</summary>
    public MarkdownToDocxException(string Message, Exception InnerException)
        : base(Message, InnerException)
    {
    }
}

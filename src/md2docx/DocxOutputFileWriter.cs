using System;
using System.IO;
using System.Windows.Forms;
using MDConverter;

namespace md2docx;

/// <summary>
/// Writes DOCX bytes with an interactive retry prompt when the output file is locked.
/// </summary>
internal static class DocxOutputFileWriter
{
    private const string LOCK_PROMPT_CAPTION = "Close document to continue";

    /// <summary>
    /// Writes DOCX bytes, prompting to close a locked file and retrying after manual dismissal of the prompt.
    /// </summary>
    /// <param name="OutputDocxPath">Destination DOCX path.</param>
    /// <param name="DocxBytes">DOCX content.</param>
    /// <param name="LockPromptTimeoutSeconds">When null, a normal message-style prompt (no auto-cancel). When set, auto-cancels after that many seconds.</param>
    public static void WriteAllBytes(string OutputDocxPath, byte[] DocxBytes, int? LockPromptTimeoutSeconds)
    {
        while (true)
        {
            try
            {
                MarkdownToDocxConverter.WriteDocxBytes(OutputDocxPath, DocxBytes);
                return;
            }
            catch (MarkdownToDocxException Ex) when (IsOutputFileLockFailure(Ex))
            {
                if (!Environment.UserInteractive)
                {
                    throw;
                }

                string Message = BuildLockPromptMessage(OutputDocxPath);
                DialogResult PromptResult = TimerMessageBox.Show(Message, LOCK_PROMPT_CAPTION, LockPromptTimeoutSeconds);

                if (PromptResult != DialogResult.OK)
                {
                    string FailureMessage = BuildLockPromptFailureMessage(OutputDocxPath, LockPromptTimeoutSeconds);
                    throw new MarkdownToDocxException(FailureMessage, Ex);
                }
            }
        }
    }

    private static string BuildLockPromptMessage(string OutputDocxPath)
    {
        string FullPath = Path.GetFullPath(OutputDocxPath);
        string FileName = Path.GetFileName(FullPath);
        return "Cannot write:\r\n" + FullPath
            + "\r\n\r\nClose \"" + FileName + "\" in Word (or any program using it), then close this message to try again.";
    }

    private static string BuildLockPromptFailureMessage(string OutputDocxPath, int? LockPromptTimeoutSeconds)
    {
        if (LockPromptTimeoutSeconds.HasValue)
        {
            return "Output file is still in use (prompt cancelled or timed out after "
                + LockPromptTimeoutSeconds.Value.ToString()
                + " seconds): "
                + OutputDocxPath;
        }

        return "Output file is still in use: " + OutputDocxPath;
    }

    private static bool IsOutputFileLockFailure(MarkdownToDocxException Ex)
    {
        return Ex.InnerException is IOException;
    }
}

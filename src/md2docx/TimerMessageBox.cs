using System;
using System.Drawing;
using System.Windows.Forms;

namespace md2docx;

/// <summary>
/// Modal message box with an optional auto-cancel countdown timer.
/// </summary>
internal sealed partial class TimerMessageBox : Form
{
    private readonly int? _timeoutSeconds;
    private int _secondsRemaining;
    private bool _timedOut;

    /// <summary>
    /// Shows a message without a countdown (normal message-style dialog).
    /// </summary>
    public static DialogResult Show(string Message, string Caption)
    {
        return Show(Message, Caption, null);
    }

    /// <summary>
    /// Shows a message with an automatic cancel countdown.
    /// </summary>
    public static DialogResult Show(string Message, string Caption, int TimeoutSeconds)
    {
        return Show(Message, Caption, (int?)TimeoutSeconds);
    }

    /// <summary>
    /// Shows a message modally.
    /// </summary>
    /// <param name="Message">Body text.</param>
    /// <param name="Caption">Window title.</param>
    /// <param name="TimeoutSeconds">When null, no timer. When set, auto-cancels after that many seconds.</param>
    public static DialogResult Show(string Message, string Caption, int? TimeoutSeconds)
    {
        using (TimerMessageBox Dialog = new TimerMessageBox(Message, Caption, TimeoutSeconds))
        {
            return Dialog.ShowDialog();
        }
    }

    private TimerMessageBox(string Message, string Caption, int? TimeoutSeconds)
    {
        InitializeComponent();

        _timeoutSeconds = TimeoutSeconds.HasValue && TimeoutSeconds.Value > 0 ? TimeoutSeconds : null;
        _secondsRemaining = _timeoutSeconds.HasValue ? _timeoutSeconds.Value : 0;
        _timedOut = false;
        bool UseTimer = _timeoutSeconds.HasValue;

        Text = Caption ?? string.Empty;
        ClientSize = UseTimer ? new Size(520, 180) : new Size(520, 152);

        _messageLabel.Size = new Size(488, UseTimer ? 72 : 96);
        _messageLabel.Text = AppendTimerHint(Message ?? string.Empty, UseTimer);

        _okButton.Location = new Point(392, UseTimer ? 128 : 104);

        if (UseTimer)
        {
            _countdownLabel.Visible = true;
            _countdownLabel.Text = BuildCountdownText(_secondsRemaining);
        }
    }

    private void TimerMessageBox_Shown(object? Sender, EventArgs EventArgs)
    {
        if (_timeoutSeconds.HasValue && _countdownTimer != null)
        {
            _countdownTimer.Start();
        }
    }

    private void OkButton_Click(object? Sender, EventArgs EventArgs)
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CountdownTimer_Tick(object? Sender, EventArgs EventArgs)
    {
        _secondsRemaining--;
        if (_secondsRemaining <= 0)
        {
            _timedOut = true;
            _countdownTimer?.Stop();
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        _countdownLabel?.Text = BuildCountdownText(_secondsRemaining);
    }

    private void TimerMessageBox_FormClosing(object? Sender, FormClosingEventArgs EventArgs)
    {
        _countdownTimer?.Stop();

        if (_timedOut)
        {
            return;
        }

        if (DialogResult != DialogResult.OK)
        {
            DialogResult = DialogResult.OK;
        }
    }

    private static string AppendTimerHint(string Message, bool UseTimer)
    {
        if (!UseTimer)
        {
            return Message;
        }

        return Message + "\r\n\r\nIf you do nothing, this prompt will cancel automatically.";
    }

    private static string BuildCountdownText(int SecondsRemaining)
    {
        return "Auto-cancel in " + SecondsRemaining.ToString() + " second"
            + (SecondsRemaining == 1 ? string.Empty : "s") + ".";
    }
}

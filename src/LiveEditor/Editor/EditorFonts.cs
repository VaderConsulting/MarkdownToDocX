using System;
using System.Drawing;
using System.Drawing.Text;

namespace LiveEditor.Editor;

/// <summary>
/// The code font for the editor and its popups. Visual Studio uses Cascadia Mono throughout, so LiveEditor does too
/// and falls back to Consolas on machines without it.
/// </summary>
internal static class EditorFonts
{
    private const string PREFERRED_FAMILY = "Cascadia Mono";

    private const string FALLBACK_FAMILY = "Consolas";

    private static readonly Lazy<string> MONOSPACE_FAMILY = new Lazy<string>(ChooseFamily);

    /// <summary>Gets the name of the monospaced family in use.</summary>
    public static string MonospaceFamily
    {
        get
        {
            return MONOSPACE_FAMILY.Value;
        }
    }

    /// <summary>Creates the code font at the given point size. The caller owns and disposes it.</summary>
    public static Font Monospace(float Size, FontStyle Style = FontStyle.Regular)
    {
        return new Font(MonospaceFamily, Size, Style, GraphicsUnit.Point);
    }

    private static string ChooseFamily()
    {
        using InstalledFontCollection Installed = new InstalledFontCollection();
        foreach (FontFamily Family in Installed.Families)
        {
            if (string.Equals(Family.Name, PREFERRED_FAMILY, StringComparison.OrdinalIgnoreCase))
            {
                return PREFERRED_FAMILY;
            }
        }

        return FALLBACK_FAMILY;
    }
}
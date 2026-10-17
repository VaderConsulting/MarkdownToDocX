using System;
using System.Runtime.CompilerServices;

/// <summary>Enables hard cell-write failures in every test assembly, including Release test runs.</summary>
internal static class CellWriteGuardTestDefaults
{
    /// <summary>Runs before tests or catalogue helpers can create a layout.</summary>
    [ModuleInitializer]
    internal static void EnableStrictCellWrites() => AppContext.SetSwitch("MermaidRecognition.Layout.ThrowOnCellPixelWrite", true);
}

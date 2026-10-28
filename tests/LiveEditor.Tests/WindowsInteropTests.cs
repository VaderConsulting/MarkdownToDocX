using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture]
public sealed class WindowsInteropTests
{
    [Test]
    public void NativeImportsResolveExportedEntryPoints()
    {
        List<string> Missing = new List<string>();
        int Checked = 0;
        foreach (Assembly Assembly in new[] { typeof(CodeEditor).Assembly, typeof(WindowsInteropTests).Assembly })
        {
            foreach (Type Type in Assembly.GetTypes())
            {
                foreach (MethodInfo Method in Type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    LibraryImportAttribute? Generated = Method.GetCustomAttribute<LibraryImportAttribute>();
                    DllImportAttribute? Runtime = Method.GetCustomAttribute<DllImportAttribute>();
                    string? Library = Generated?.LibraryName ?? Runtime?.Value;
                    if (Library == null) continue;

                    Checked++;
                    string EntryPoint = Generated?.EntryPoint ?? Runtime?.EntryPoint ?? Method.Name;
                    IntPtr Module = NativeLibrary.Load(Library);
                    try
                    {
                        bool Found = NativeLibrary.TryGetExport(Module, EntryPoint, out _);
                        // Runtime P/Invoke can append A/W; source-generated imports use exact names.
                        if (!Found && Generated == null && Runtime is { ExactSpelling: false })
                        {
                            string Suffix = Runtime.CharSet is CharSet.Unicode or CharSet.Auto ? "W" : "A";
                            Found = NativeLibrary.TryGetExport(Module, EntryPoint + Suffix, out _);
                        }
                        if (!Found) Missing.Add(Type.FullName + "." + Method.Name + ": " + Library + "!" + EntryPoint);
                    }
                    finally
                    {
                        NativeLibrary.Free(Module);
                    }
                }
            }
        }
        Assert.That(Checked, Is.GreaterThan(0), "No native imports were inspected.");
        Assert.That(Missing, Is.Empty, string.Join(Environment.NewLine, Missing));
    }
}

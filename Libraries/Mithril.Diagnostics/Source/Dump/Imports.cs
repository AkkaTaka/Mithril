namespace Mithril.Diagnostics.Dump;

using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

internal static class Imports
{
  // DbgHelp.dll
  [DllImport("Dbghelp.dll", SetLastError = true)]
  public static extern bool MiniDumpWriteDump(
    IntPtr hProcess,
    uint processId,
    SafeFileHandle hFile,
    MiniDumpType dumpType,
    IntPtr exceptionParam,
    IntPtr userStreamParam,
    IntPtr callbackParam);
}

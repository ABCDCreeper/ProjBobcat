﻿using System;
using System.Runtime.InteropServices;
using System.Security;

[SecurityCritical]
[SuppressUnmanagedCodeSecurity]
static class NativeMethods
{
    [DllImport("User32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SetWindowText(IntPtr winHandle, string title);
}
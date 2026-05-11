using System.Management;
using System.Runtime.Versioning;

namespace WhatCable.Providers.Windows.Wmi;

/// Thin wrapper around ManagementObjectSearcher to keep call sites
/// concise and make it possible to fake in tests. Stays internal.
[SupportedOSPlatform("windows")]
internal static class WmiQueryHelper
{
    public static IEnumerable<ManagementObject> Query(string @namespace, string wql)
    {
        // WMI is lazy: ManagementException can throw at construction time
        // (bad namespace), at Get() time (bad query), OR at iteration time
        // (class not found in namespace). We materialize inside the catch
        // so callers never see WMI exceptions — Phase 1 providers must
        // fail-closed for missing classes.
        List<ManagementObject> buffer = [];
        try
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope(@namespace), new ObjectQuery(wql));
            using var results = searcher.Get();
            foreach (ManagementObject obj in results.Cast<ManagementObject>())
                buffer.Add(obj);
        }
        catch (ManagementException) { }
        catch (UnauthorizedAccessException) { }
        catch (System.Runtime.InteropServices.COMException) { }
        return buffer;
    }

    /// True if a WMI namespace exists and is accessible to the current user.
    public static bool NamespaceExists(string @namespace)
    {
        try
        {
            var scope = new ManagementScope(@namespace);
            scope.Connect();
            return scope.IsConnected;
        }
        catch
        {
            return false;
        }
    }

    public static string? GetString(ManagementBaseObject obj, string property)
    {
        try { return obj[property]?.ToString(); }
        catch { return null; }
    }

    public static T? Get<T>(ManagementBaseObject obj, string property) where T : struct
    {
        try
        {
            var value = obj[property];
            return value is null ? null : (T?)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return null;
        }
    }
}

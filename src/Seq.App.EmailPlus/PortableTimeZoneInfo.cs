using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Seq.App.EmailPlus;

static class PortableTimeZoneInfo
{
    public const string UtcTimeZoneName = "Etc/UTC";
    
    public static bool IsUsingNlsOnWindows()
    {
        // Whether ICU is used on Windows depends on both the Windows version and the .NET version. When ICU is
        // unavailable, .NET falls back to NLS, which is only aware of Windows time zone names.
        // See: https://github.com/dotnet/docs/issues/30319
            
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;
            
        // https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu#determine-if-your-app-is-using-icu
        var sortVersion = CultureInfo.InvariantCulture.CompareInfo.Version;
        var bytes = sortVersion.SortId.ToByteArray();
        var version = bytes[3] << 24 | bytes[2] << 16 | bytes[1] << 8 | bytes[0];
        var isIcu = version != 0 && version == sortVersion.FullVersion;

        return !isIcu;
    }

    public static TimeZoneInfo FindSystemTimeZoneById(string timeZoneId)
    {
        if (IsUsingNlsOnWindows() && timeZoneId == UtcTimeZoneName)
        {
            // Etc/UTC is the default; this keeps the default template working even without ICU.
            return TimeZoneInfo.Utc;
        }
        
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }
}
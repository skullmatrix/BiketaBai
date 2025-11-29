using System;

namespace BiketaBai.Helpers;

/// <summary>
/// Helper class for timezone conversions, specifically for Philippine Time (PHT/PHST)
/// Philippine Time is UTC+8 (no daylight saving time)
/// </summary>
public static class TimeZoneHelper
{
    private static readonly TimeZoneInfo PhilippineTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Singapore Standard Time" : "Asia/Manila"
    );

    /// <summary>
    /// Converts UTC DateTime to Philippine Time (UTC+8)
    /// </summary>
    public static DateTime ToPhilippineTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Unspecified)
        {
            // Assume it's UTC if unspecified
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }
        
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, PhilippineTimeZone);
    }

    /// <summary>
    /// Converts Philippine Time to UTC
    /// </summary>
    public static DateTime ToUtc(DateTime philippineTime)
    {
        if (philippineTime.Kind == DateTimeKind.Unspecified)
        {
            // Assume it's Philippine time if unspecified
            philippineTime = DateTime.SpecifyKind(philippineTime, DateTimeKind.Unspecified);
        }
        
        return TimeZoneInfo.ConvertTimeToUtc(philippineTime, PhilippineTimeZone);
    }

    /// <summary>
    /// Gets current Philippine time
    /// </summary>
    public static DateTime GetPhilippineTimeNow()
    {
        return ToPhilippineTime(DateTime.UtcNow);
    }

    /// <summary>
    /// Formats a DateTime to Philippine time string
    /// </summary>
    public static string FormatPhilippineTime(DateTime utcDateTime, string format = "MMM dd, yyyy hh:mm tt")
    {
        var phTime = ToPhilippineTime(utcDateTime);
        return phTime.ToString(format);
    }

    /// <summary>
    /// Formats a DateTime to ISO 8601 string in Philippine time (for JavaScript)
    /// </summary>
    public static string FormatPhilippineTimeIso(DateTime utcDateTime)
    {
        var phTime = ToPhilippineTime(utcDateTime);
        return phTime.ToString("yyyy-MM-ddTHH:mm:ss");
    }
}


namespace DevSticky.Helpers;

/// <summary>
/// Helper class for DateTime operations and formatting.
/// Provides utilities for common date/time operations and formatting patterns.
/// </summary>
public static class DateTimeHelper
{
    /// <summary>
    /// Standard date format: yyyy-MM-dd
    /// </summary>
    public const string StandardDateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Standard datetime format: yyyy-MM-dd HH:mm:ss
    /// </summary>
    public const string StandardDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// Short datetime format: yyyy-MM-dd HH:mm
    /// </summary>
    public const string ShortDateTimeFormat = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// Filename-safe datetime format: yyyyMMdd_HHmmss
    /// </summary>
    public const string FilenameDateTimeFormat = "yyyyMMdd_HHmmss";

    /// <summary>
    /// ISO 8601 format: yyyy-MM-ddTHH:mm:ss.fffZ
    /// </summary>
    public const string Iso8601Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

    /// <summary>
    /// Formats a DateTime to standard date format (yyyy-MM-dd).
    /// </summary>
    /// <param name="dateTime">The DateTime to format</param>
    /// <returns>The formatted date string</returns>
    public static string ToStandardDate(DateTime dateTime)
    {
        return dateTime.ToString(StandardDateFormat);
    }

    /// <summary>
    /// Formats a DateTime to standard datetime format (yyyy-MM-dd HH:mm:ss).
    /// </summary>
    /// <param name="dateTime">The DateTime to format</param>
    /// <returns>The formatted datetime string</returns>
    public static string ToStandardDateTime(DateTime dateTime)
    {
        return dateTime.ToString(StandardDateTimeFormat);
    }

    /// <summary>
    /// Formats a DateTime to short datetime format (yyyy-MM-dd HH:mm).
    /// </summary>
    /// <param name="dateTime">The DateTime to format</param>
    /// <returns>The formatted datetime string</returns>
    public static string ToShortDateTime(DateTime dateTime)
    {
        return dateTime.ToString(ShortDateTimeFormat);
    }

    /// <summary>
    /// Formats a DateTime to filename-safe format (yyyyMMdd_HHmmss).
    /// </summary>
    /// <param name="dateTime">The DateTime to format</param>
    /// <returns>The formatted datetime string safe for filenames</returns>
    public static string ToFilenameDateTime(DateTime dateTime)
    {
        return dateTime.ToString(FilenameDateTimeFormat);
    }

    /// <summary>
    /// Formats a DateTime to ISO 8601 format.
    /// </summary>
    /// <param name="dateTime">The DateTime to format</param>
    /// <returns>The formatted datetime string in ISO 8601 format</returns>
    public static string ToIso8601(DateTime dateTime)
    {
        return dateTime.ToUniversalTime().ToString(Iso8601Format);
    }

    /// <summary>
    /// Gets a relative time string (e.g., "2 hours ago", "3 days ago").
    /// </summary>
    /// <param name="dateTime">The DateTime to compare</param>
    /// <param name="relativeTo">The DateTime to compare against (default: now)</param>
    /// <returns>A human-readable relative time string</returns>
    public static string ToRelativeTime(DateTime dateTime, DateTime? relativeTo = null)
    {
        var now = relativeTo ?? DateTime.UtcNow;
        var timeSpan = now - dateTime;

        if (timeSpan.TotalSeconds < 60)
            return "just now";
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes} minute{(timeSpan.TotalMinutes >= 2 ? "s" : "")} ago";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours} hour{(timeSpan.TotalHours >= 2 ? "s" : "")} ago";
        if (timeSpan.TotalDays < 7)
            return $"{(int)timeSpan.TotalDays} day{(timeSpan.TotalDays >= 2 ? "s" : "")} ago";
        if (timeSpan.TotalDays < 30)
            return $"{(int)(timeSpan.TotalDays / 7)} week{(timeSpan.TotalDays >= 14 ? "s" : "")} ago";
        if (timeSpan.TotalDays < 365)
            return $"{(int)(timeSpan.TotalDays / 30)} month{(timeSpan.TotalDays >= 60 ? "s" : "")} ago";

        return $"{(int)(timeSpan.TotalDays / 365)} year{(timeSpan.TotalDays >= 730 ? "s" : "")} ago";
    }

    /// <summary>
    /// Checks if a DateTime is today.
    /// </summary>
    /// <param name="dateTime">The DateTime to check</param>
    /// <returns>True if the DateTime is today; otherwise false</returns>
    public static bool IsToday(DateTime dateTime)
    {
        return dateTime.Date == DateTime.Today;
    }

    /// <summary>
    /// Checks if a DateTime is yesterday.
    /// </summary>
    /// <param name="dateTime">The DateTime to check</param>
    /// <returns>True if the DateTime is yesterday; otherwise false</returns>
    public static bool IsYesterday(DateTime dateTime)
    {
        return dateTime.Date == DateTime.Today.AddDays(-1);
    }

    /// <summary>
    /// Checks if a DateTime is within the current week.
    /// </summary>
    /// <param name="dateTime">The DateTime to check</param>
    /// <returns>True if the DateTime is within the current week; otherwise false</returns>
    public static bool IsThisWeek(DateTime dateTime)
    {
        var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
        var endOfWeek = startOfWeek.AddDays(7);
        return dateTime.Date >= startOfWeek && dateTime.Date < endOfWeek;
    }

    /// <summary>
    /// Gets the start of the day (00:00:00) for a given DateTime.
    /// </summary>
    /// <param name="dateTime">The DateTime to process</param>
    /// <returns>The DateTime at the start of the day</returns>
    public static DateTime StartOfDay(DateTime dateTime)
    {
        return dateTime.Date;
    }

    /// <summary>
    /// Gets the end of the day (23:59:59.999) for a given DateTime.
    /// </summary>
    /// <param name="dateTime">The DateTime to process</param>
    /// <returns>The DateTime at the end of the day</returns>
    public static DateTime EndOfDay(DateTime dateTime)
    {
        return dateTime.Date.AddDays(1).AddTicks(-1);
    }

    /// <summary>
    /// Calculates the age in years from a birth date.
    /// </summary>
    /// <param name="birthDate">The birth date</param>
    /// <param name="referenceDate">The reference date (default: today)</param>
    /// <returns>The age in years</returns>
    public static int CalculateAge(DateTime birthDate, DateTime? referenceDate = null)
    {
        var reference = referenceDate ?? DateTime.Today;
        var age = reference.Year - birthDate.Year;
        
        if (birthDate.Date > reference.AddYears(-age))
            age--;

        return age;
    }

    /// <summary>
    /// Tries to parse a string as a DateTime using multiple common formats.
    /// </summary>
    /// <param name="value">The string to parse</param>
    /// <param name="result">The parsed DateTime if successful</param>
    /// <returns>True if parsing was successful; otherwise false</returns>
    public static bool TryParseFlexible(string? value, out DateTime result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = DateTime.MinValue;
            return false;
        }

        // Try standard parsing first
        if (DateTime.TryParse(value, out result))
            return true;

        // Try specific formats
        string[] formats = 
        {
            StandardDateFormat,
            StandardDateTimeFormat,
            ShortDateTimeFormat,
            FilenameDateTimeFormat,
            Iso8601Format
        };

        return DateTime.TryParseExact(value, formats, 
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out result);
    }
}

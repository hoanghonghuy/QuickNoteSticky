using DevSticky.Interfaces;

namespace DevSticky.Services;

/// <summary>
/// System implementation of date/time provider
/// </summary>
public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime Now => DateTime.Now;
}

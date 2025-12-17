using DevSticky.Interfaces;
using System;
using System.Threading.Tasks;

namespace DevSticky.Services;

/// <summary>
/// Default implementation of ITaskScheduler that uses Task.Delay
/// </summary>
public class DefaultTaskScheduler : ITaskScheduler
{
    public Task Delay(int milliseconds, System.Threading.CancellationToken cancellationToken)
    {
        return Task.Delay(milliseconds, cancellationToken);
    }
}

using System;
using System.Threading.Tasks;

namespace DevSticky.Interfaces;

/// <summary>
/// Interface for scheduling tasks with delay, allowing for testable implementations
/// </summary>
public interface ITaskScheduler
{
    /// <summary>
    /// Schedule a task to run after the specified delay
    /// </summary>
    Task Delay(int milliseconds, System.Threading.CancellationToken cancellationToken);
}

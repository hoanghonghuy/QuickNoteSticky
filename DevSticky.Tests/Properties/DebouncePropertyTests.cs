using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;
using System.Collections.Concurrent;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for DebounceService cancellation behavior
/// **Feature: code-refactor, Property 8: Debounce Cancellation**
/// **Validates: Requirements 5.1**
/// </summary>
public class DebouncePropertyTests
{
    /// <summary>
    /// Property 8: Debounce Cancellation
    /// For any debounced action, calling debounce again before delay expires 
    /// should cancel the previous action and only execute the latest one.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DebounceService_WhenCalledMultipleTimes_ShouldCancelPreviousAndExecuteLatest()
    {
        return Prop.ForAll(
            DebounceTestDataGenerator(),
            data =>
            {
                var (key, delays, totalWaitTime) = data;
                
                using var service = new DebounceService();
                using var resetEvent = new ManualResetEventSlim(false);
                
                var executionCount = 0;
                var lastExecutedDelay = -1;
                
                // Schedule multiple debounce calls with different delays
                for (int i = 0; i < delays.Count; i++)
                {
                    var currentIndex = i;
                    var currentDelay = delays[i];
                    
                    service.Debounce(key, () =>
                    {
                        Interlocked.Increment(ref executionCount);
                        lastExecutedDelay = currentDelay;
                        resetEvent.Set();
                    }, currentDelay);
                    
                    // Small gap between calls to ensure they're processed separately
                    Thread.Sleep(10);
                }
                
                // Wait for execution
                var executed = resetEvent.Wait(totalWaitTime);
                
                // Give a bit more time for any potential additional executions
                Thread.Sleep(100);
                
                // Only the last debounce call should execute
                var onlyLastExecuted = executionCount == 1;
                var correctDelayExecuted = lastExecutedDelay == delays.Last();
                
                return (executed && onlyLastExecuted && correctDelayExecuted)
                    .ToProperty()
                    .Label($"Expected 1 execution with delay {delays.Last()}, got {executionCount} executions with delay {lastExecutedDelay}");
            });
    }
    
    /// <summary>
    /// Property: Explicit Cancel Prevents Execution
    /// For any debounced action, calling Cancel() before the delay expires
    /// should prevent the action from executing.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DebounceService_WhenExplicitlyCancelled_ShouldNotExecute()
    {
        return Prop.ForAll(
            SingleDebounceTestDataGenerator(),
            data =>
            {
                var (key, delay) = data;
                
                using var service = new DebounceService();
                
                var executed = false;
                
                // Schedule debounce
                service.Debounce(key, () => executed = true, delay);
                
                // Cancel before delay expires
                Thread.Sleep(Math.Max(1, delay / 4)); // Wait 1/4 of the delay
                service.Cancel(key);
                
                // Wait past the original delay
                Thread.Sleep(delay + 100);
                
                return (!executed)
                    .ToProperty()
                    .Label($"Action should not execute after explicit cancellation");
            });
    }
    
    /// <summary>
    /// Property: Multiple Keys Are Independent
    /// For any set of different keys, debouncing actions with different keys
    /// should not interfere with each other.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DebounceService_WithDifferentKeys_ShouldBeIndependent()
    {
        return Prop.ForAll(
            MultipleKeysTestDataGenerator(),
            data =>
            {
                var (keys, delay) = data;
                
                using var service = new DebounceService();
                
                var executionCounts = new ConcurrentDictionary<string, int>();
                var resetEvents = keys.ToDictionary(k => k, _ => new ManualResetEventSlim(false));
                
                try
                {
                    // Schedule debounce for each key
                    foreach (var key in keys)
                    {
                        service.Debounce(key, () =>
                        {
                            executionCounts.AddOrUpdate(key, 1, (_, count) => count + 1);
                            resetEvents[key].Set();
                        }, delay);
                    }
                    
                    // Wait for all executions
                    var allExecuted = resetEvents.Values.All(e => e.Wait(delay + 200));
                    
                    // Each key should execute exactly once
                    var allExecutedOnce = keys.All(k => executionCounts.GetValueOrDefault(k, 0) == 1);
                    
                    return (allExecuted && allExecutedOnce)
                        .ToProperty()
                        .Label($"All {keys.Count} keys should execute exactly once");
                }
                finally
                {
                    foreach (var resetEvent in resetEvents.Values)
                    {
                        resetEvent.Dispose();
                    }
                }
            });
    }
    
    /// <summary>
    /// Property: Rapid Successive Calls Only Execute Last
    /// For any key with rapid successive debounce calls, only the last call should execute.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DebounceService_WithRapidSuccessiveCalls_ShouldOnlyExecuteLast()
    {
        return Prop.ForAll(
            RapidCallsTestDataGenerator(),
            data =>
            {
                var (key, delay, callCount) = data;
                
                using var service = new DebounceService();
                using var resetEvent = new ManualResetEventSlim(false);
                
                var executionCount = 0;
                var lastCallIndex = -1;
                
                // Make rapid successive calls
                for (int i = 0; i < callCount; i++)
                {
                    var currentIndex = i;
                    service.Debounce(key, () =>
                    {
                        Interlocked.Increment(ref executionCount);
                        lastCallIndex = currentIndex;
                        resetEvent.Set();
                    }, delay);
                    
                    // Very small delay between calls (much less than debounce delay)
                    Thread.Sleep(1);
                }
                
                // Wait for execution
                var executed = resetEvent.Wait(delay + 200);
                
                // Give time for any additional executions
                Thread.Sleep(100);
                
                // Should execute exactly once, and it should be the last call
                var executedOnce = executionCount == 1;
                var wasLastCall = lastCallIndex == callCount - 1;
                
                return (executed && executedOnce && wasLastCall)
                    .ToProperty()
                    .Label($"Expected 1 execution of call {callCount - 1}, got {executionCount} executions of call {lastCallIndex}");
            });
    }

    /// <summary>
    /// Generates test data for multiple debounce calls with the same key.
    /// Returns (key, delays, totalWaitTime) where delays is a list of delay values.
    /// </summary>
    private static Arbitrary<(string key, List<int> delays, int totalWaitTime)> DebounceTestDataGenerator()
    {
        var gen = from key in Arb.Generate<NonEmptyString>()
                  from callCount in Gen.Choose(2, 5) // 2-5 calls
                  from delays in Gen.ListOf(callCount, Gen.Choose(50, 300)) // 50-300ms delays
                  let delaysList = delays.ToList()
                  let maxDelay = delaysList.Max()
                  let totalWaitTime = maxDelay + 500 // Wait longer than max delay
                  select (key.Get, delaysList, totalWaitTime);

        return Arb.From(gen);
    }
    
    /// <summary>
    /// Generates test data for a single debounce call.
    /// Returns (key, delay).
    /// </summary>
    private static Arbitrary<(string key, int delay)> SingleDebounceTestDataGenerator()
    {
        var gen = from key in Arb.Generate<NonEmptyString>()
                  from delay in Gen.Choose(100, 500) // 100-500ms delay
                  select (key.Get, delay);

        return Arb.From(gen);
    }
    
    /// <summary>
    /// Generates test data for multiple independent keys.
    /// Returns (keys, delay) where keys is a list of unique keys.
    /// </summary>
    private static Arbitrary<(List<string> keys, int delay)> MultipleKeysTestDataGenerator()
    {
        var gen = from keyCount in Gen.Choose(2, 5) // 2-5 keys
                  from keys in Gen.ListOf(keyCount, Arb.Generate<NonEmptyString>())
                  from delay in Gen.Choose(100, 300) // 100-300ms delay
                  let uniqueKeys = keys.Select(k => k.Get).Distinct().ToList()
                  where uniqueKeys.Count >= 2 // Ensure we have at least 2 unique keys
                  select (uniqueKeys.Take(keyCount).ToList(), delay);

        return Arb.From(gen);
    }
    
    /// <summary>
    /// Generates test data for rapid successive calls.
    /// Returns (key, delay, callCount).
    /// </summary>
    private static Arbitrary<(string key, int delay, int callCount)> RapidCallsTestDataGenerator()
    {
        var gen = from key in Arb.Generate<NonEmptyString>()
                  from delay in Gen.Choose(100, 300) // 100-300ms delay
                  from callCount in Gen.Choose(3, 10) // 3-10 rapid calls
                  select (key.Get, delay, callCount);

        return Arb.From(gen);
    }
}
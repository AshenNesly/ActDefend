using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ActDefend.Evaluation;

public class PerformanceMonitor : IDisposable
{
    private readonly Process _process;
    private readonly CancellationTokenSource _cts = new();
    private Task? _monitorTask;
    
    private readonly List<double> _cpuSamples = new();
    private readonly List<double> _memoryMbSamples = new();
    
    private TimeSpan _lastTotalProcessorTime;
    private DateTimeOffset _lastSampleTime;
    
    public PerformanceMonitor(int processId)
    {
        _process = Process.GetProcessById(processId);
    }
    
    public void Start(int intervalMs = 500)
    {
        _lastTotalProcessorTime = _process.TotalProcessorTime;
        _lastSampleTime = DateTimeOffset.UtcNow;
        
        _monitorTask = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                await Task.Delay(intervalMs, _cts.Token).ConfigureAwait(false);
                Sample();
            }
        });
    }
    
    public void Stop()
    {
        _cts.Cancel();
        try
        {
            _monitorTask?.Wait(1000);
        }
        catch (AggregateException)
        {
            // Expected cancellation
        }
        
        // Final sample
        Sample();
    }
    
    private void Sample()
    {
        try
        {
            _process.Refresh();
            if (_process.HasExited) return;

            var currentProcessorTime = _process.TotalProcessorTime;
            var currentTime = DateTimeOffset.UtcNow;
            
            var cpuUsedMs = (currentProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
            var totalMsPassed = (currentTime - _lastSampleTime).TotalMilliseconds;
            
            if (totalMsPassed > 0)
            {
                var cpuUsage = (cpuUsedMs / (Environment.ProcessorCount * totalMsPassed)) * 100.0;
                lock (_cpuSamples)
                {
                    _cpuSamples.Add(cpuUsage);
                }
            }
            
            var memMb = _process.WorkingSet64 / (1024.0 * 1024.0);
            lock (_memoryMbSamples)
            {
                _memoryMbSamples.Add(memMb);
            }
            
            _lastTotalProcessorTime = currentProcessorTime;
            _lastSampleTime = currentTime;
        }
        catch (Exception)
        {
            // Process might have exited while sampling
        }
    }
    
    public double AverageCpuUsagePercent => _cpuSamples.Count > 0 ? _cpuSamples.Average() : 0;
    public double PeakCpuUsagePercent => _cpuSamples.Count > 0 ? _cpuSamples.Max() : 0;
    
    public double AverageMemoryMb => _memoryMbSamples.Count > 0 ? _memoryMbSamples.Average() : 0;
    public double PeakMemoryMb => _memoryMbSamples.Count > 0 ? _memoryMbSamples.Max() : 0;

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _process.Dispose();
    }
}

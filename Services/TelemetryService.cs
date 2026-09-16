using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AdminWorks.Models;

namespace AdminWorks.Services
{
    public class TelemetryService : IDisposable
    {
        private readonly Timer _timer;
        private PerformanceCounter? _cpuCounter;
        public event Action<TelemetrySnapshot>? SnapshotUpdated;

        public TelemetryService()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue();
            }
            catch
            {
                _cpuCounter = null;
            }

            _timer = new Timer(_ => UpdateMetrics(), null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }

        private void UpdateMetrics()
        {
            var snapshot = new TelemetrySnapshot();

            // CPU Load
            try
            {
                if (_cpuCounter != null)
                {
                    snapshot.CpuLoad = Math.Round(_cpuCounter.NextValue(), 0);
                }
            }
            catch { }

            // Memory Status via GlobalMemoryStatusEx (0% WMI overhead)
            try
            {
                var mem = new NativeMethods.MEMORYSTATUSEX();
                mem.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(mem);
                if (NativeMethods.GlobalMemoryStatusEx(ref mem))
                {
                    snapshot.RamTotalGb = Math.Round(mem.ullTotalPhys / (1024.0 * 1024 * 1024), 1);
                    snapshot.RamUsedGb = Math.Round((mem.ullTotalPhys - mem.ullAvailPhys) / (1024.0 * 1024 * 1024), 1);
                }
            }
            catch { }

            // System Drive C:
            try
            {
                var drive = new DriveInfo("C");
                if (drive.IsReady)
                {
                    snapshot.DiskTotalGb = Math.Round(drive.TotalSize / (1024.0 * 1024 * 1024), 1);
                    snapshot.DiskFreeGb = Math.Round(drive.AvailableFreeSpace / (1024.0 * 1024 * 1024), 1);
                }
            }
            catch { }

            // System Uptime via TickCount64
            snapshot.Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

            SnapshotUpdated?.Invoke(snapshot);
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _cpuCounter?.Dispose();
        }
    }
}

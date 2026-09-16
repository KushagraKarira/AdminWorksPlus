using System;

namespace AdminWorks.Models
{
    public class TelemetrySnapshot
    {
        public double CpuLoad { get; set; }
        public double RamUsedGb { get; set; }
        public double RamTotalGb { get; set; }
        public double DiskFreeGb { get; set; }
        public double DiskTotalGb { get; set; }
        public TimeSpan Uptime { get; set; }

        public double RamPercentage => RamTotalGb > 0 ? (RamUsedGb / RamTotalGb) * 100 : 0;
        public double DiskPercentage => DiskTotalGb > 0 ? ((DiskTotalGb - DiskFreeGb) / DiskTotalGb) * 100 : 0;
    }
}

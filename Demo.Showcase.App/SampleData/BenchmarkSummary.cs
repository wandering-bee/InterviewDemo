using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleData
{
    public record BenchmarkSummary
    {
        public double[] Latencies { get; init; } = Array.Empty<double>();
        public double Min { get; init; }
        public double P50 { get; init; }
        public double Avg { get; init; }
        public double P90 { get; init; }
        public double P99 { get; init; }
        public double Max { get; init; }
        public double TotalSec { get; init; }
    }
}

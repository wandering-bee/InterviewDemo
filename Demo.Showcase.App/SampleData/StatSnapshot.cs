using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleData
{
    public record StatSnapshot
    {
        public int Done { get; init; }
        public double Last { get; init; }
        public double Avg { get; init; }
        public double Elapsed { get; init; }
        public double[] LatSlice { get; init; } = Array.Empty<double>();
    }

}

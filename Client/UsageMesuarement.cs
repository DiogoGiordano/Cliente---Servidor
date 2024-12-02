using System.Diagnostics;
using System.Management;

public class UsageMesuarement
{
    private List<long> _cpuSamples = new();
    private List<long> _memorySamples = new();
    private Process _process;
    private bool _isMonitoring = false;

    public UsageMesuarement()
    {
        _process = Process.GetCurrentProcess();
    }

    public void StartMonitoring()
    {
        _isMonitoring = true;
        Task.Run(async () =>
        {
            while (_isMonitoring)
            {
                AddSample();
                await Task.Delay(100); // Frequência de amostragem
            }
        });
    }

    public void StopMonitoring()
    {
        _isMonitoring = false;
    }

    public void AddSample()
    {
        _cpuSamples.Add((long)_process.TotalProcessorTime.TotalMilliseconds);
        _memorySamples.Add(_process.WorkingSet64 / (1024 * 1024)); // Memória em MB

    }

    public object GetMetricsSummary()
    {
        var maxFrequencyGHz = GetCpuMaxFrequency(); 

        // Calcula frequências utilizadas (baseado nos usos percentuais de _cpuSamples)
        var frequenciesUsed = _cpuSamples.Select(usage => CalculateUsedFrequency(usage, maxFrequencyGHz)).ToList();

        return new
        {
            CPU = new
            {
                Min = _cpuSamples.Min() + " Milissegundos",
                Max = _cpuSamples.Max()+ " Milissegundos",
                Average = _cpuSamples.Average()+ " Milissegundos",
                Median = GetMedian(_cpuSamples)+ " Milissegundos",
                StdDev = GetStandardDeviation(_cpuSamples) + " Milissegundos",
                MaxFrequencyGHz = maxFrequencyGHz + " GHz", // Frequência máxima
                FrequencyUsed = new
                {
                    Mini = frequenciesUsed.Min() + " GHz",
                    Maxi = frequenciesUsed.Max() + " GHz",
                    Average = frequenciesUsed.Average() + " GHz",
                    Median = GetMedian(frequenciesUsed.Select(f => (long)(f * 1000)).ToList()) / 1000.0 + " GHz", // converte pra GHz
                    StdDev = GetStandardDeviation(frequenciesUsed.Select(f => (long)(f * 1000)).ToList()) / 1000.0 + " GHz" // converte pra GHz
                }
            },
            Memory = new
            {
                Min = _memorySamples.Min(),
                Max = _memorySamples.Max(),
                Average = _memorySamples.Average(),
                Median = GetMedian(_memorySamples),
                StdDev = GetStandardDeviation(_memorySamples)
            }
        };
    }

    private double GetCpuMaxFrequency()
    {
        double maxFrequency = 0; // Armazenará a frequência máxima
        using (var searcher = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor"))
        {
            foreach (var obj in searcher.Get())
            {
                maxFrequency = Math.Max(maxFrequency, Convert.ToDouble(obj["MaxClockSpeed"]));
            }
        }

        return maxFrequency / 1000.0; // Convertendo de MHz para GHz
    }

    private double CalculateUsedFrequency(double cpuUsagePercentage, double maxFrequencyGHz)
    {
        return (cpuUsagePercentage / 100.0) * maxFrequencyGHz;
    }



    private static double GetMedian(List<long> source)
    {
        var sorted = source.OrderBy(x => x).ToList();
        int count = sorted.Count;
        if (count == 0) return 0.0; // Evita erro se a lista estiver vazia
        return count % 2 == 0
            ? (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0
            : sorted[count / 2];
    }


    private static double GetStandardDeviation(List<long> source)
    {
        if (source.Count == 0) return 0.0; // Evita divisão por zero
        double avg = source.Average();
        return Math.Sqrt(source.Average(v => Math.Pow(v - avg, 2)));
    }

}
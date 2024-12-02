using System.Diagnostics;
using System.Management;

public class UsageMeasurement
{
    private List<double> _cpuSamples = new(); // Uso percentual da CPU (0 a 100)
    private List<long> _memorySamples = new(); // Uso de memoria em MB
    private Process _process;
    private bool _isMonitoring = false;

    private TimeSpan _lastCpuTime = TimeSpan.Zero; // Ultima leitura do tempo total de CPU
    private DateTime _lastSampleTime = DateTime.MinValue; // Tempo da ultima amostra

    public UsageMeasurement()
    {
        _process = Process.GetCurrentProcess();
    }

    public void StartMonitoring()
    {
        _isMonitoring = true;
        _lastCpuTime = _process.TotalProcessorTime;
        _lastSampleTime = DateTime.Now;

        Task.Run(async () =>
        {
            while (_isMonitoring)
            {
                AddSample();
                await Task.Delay(100); // Frequencia de amostragem (100ms)
            }
        });
    }

    public void StopMonitoring()
    {
        _isMonitoring = false;
    }

    public void AddSample()
    {
        // Calcula o uso percentual da CPU em relacao ao intervalo de tempo amostrado
        double cpuUsage = GetCurrentCpuUsage();
        _cpuSamples.Add(cpuUsage);

        // Adiciona o uso de memoria em MB
        _memorySamples.Add(_process.WorkingSet64 / (1024 * 1024));
    }

    public object GetMetricsSummary()
    {
        var maxFrequencyGHz = GetCpuMaxFrequency(); // Frequencia maxima da CPU em GHz

        // Calcula frequencias utilizadas (com base no percentual da CPU)
        var frequenciesUsed = _cpuSamples
            .Select(usage => CalculateUsedFrequency(usage, maxFrequencyGHz))
            .ToList();

        return new
        {
            CPU = new
            {
                Min = _cpuSamples.Min() + " % (Uso minimo da CPU)",
                Max = _cpuSamples.Max() + " % (Uso maximo da CPU)",
                Average = _cpuSamples.Average() + " % (Uso medio da CPU)",
                Median = GetMedian(_cpuSamples) + " % (Uso mediano da CPU)",
                StdDev = GetStandardDeviation(_cpuSamples) + " % (Desvio padrao do uso da CPU)",
                MaxFrequencyGHz = maxFrequencyGHz + " GHz (Frequencia maxima da CPU)",
                FrequencyUsed = new
                {
                    Mini = frequenciesUsed.Min() + " GHz (Frequencia minima usada)",
                    Maxi = frequenciesUsed.Max() + " GHz (Frequencia maxima usada)",
                    Average = frequenciesUsed.Average() + " GHz (Frequencia media usada)",
                    Median = GetMedian(frequenciesUsed) + " GHz (Frequencia mediana usada)",
                    StdDev = GetStandardDeviation(frequenciesUsed) + " GHz (Desvio padrao da frequencia usada)"
                }
            },
            Memory = new
            {
                Min = _memorySamples.Min() + " MB (Uso minimo de memoria)",
                Max = _memorySamples.Max() + " MB (Uso maximo de memoria)",
                Average = _memorySamples.Average() + " MB (Uso medio de memoria)",
                Median = GetMedian(_memorySamples) + " MB (Uso mediano de memoria)",
                StdDev = GetStandardDeviation(_memorySamples) + " MB (Desvio padrao do uso de memoria)"
            }
        };
    }

    private double GetCurrentCpuUsage()
    {
        var currentCpuTime = _process.TotalProcessorTime;
        var currentTime = DateTime.Now;

        if (_lastSampleTime == DateTime.MinValue)
        {
            // Primeira execucao, inicializa mas nao calcula
            _lastCpuTime = currentCpuTime;
            _lastSampleTime = currentTime;
            return 0.0;
        }

        // Calcula o tempo decorrido
        double elapsedTimeMs = (currentTime - _lastSampleTime).TotalMilliseconds;
        double elapsedCpuTimeMs = (currentCpuTime - _lastCpuTime).TotalMilliseconds;

        // Atualiza os valores de referencia para o proximo calculo
        _lastCpuTime = currentCpuTime;
        _lastSampleTime = currentTime;

        // Calcula o uso percentual da CPU em relacao ao intervalo e ao numero de nucleos
        return Math.Min((elapsedCpuTimeMs / (elapsedTimeMs * Environment.ProcessorCount)) * 100.0, 100.0);
    }

    private double GetCpuMaxFrequency()
    {
        double maxFrequency = 0; // Frequencia maxima do processador em MHz
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
        // Calcula a frequencia utilizada com base no percentual de uso da CPU
        return (cpuUsagePercentage / 100.0) * maxFrequencyGHz;
    }

    private static double GetMedian(List<double> source)
    {
        if (source.Count == 0) return 0.0; // Evita erro se a lista estiver vazia
        var sorted = source.OrderBy(x => x).ToList();
        int count = sorted.Count;

        return count % 2 == 0
            ? (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0
            : sorted[count / 2];
    }

    private static double GetMedian(List<long> source)
    {
        if (source.Count == 0) return 0.0; // Evita erro se a lista estiver vazia
        var sorted = source.OrderBy(x => x).ToList();
        int count = sorted.Count;

        return count % 2 == 0
            ? (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0
            : sorted[count / 2];
    }

    private static double GetStandardDeviation(List<double> source)
    {
        if (source.Count == 0) return 0.0; // Evita divisao por zero
        double avg = source.Average();
        return Math.Sqrt(source.Average(v => Math.Pow(v - avg, 2)));
    }

    private static double GetStandardDeviation(List<long> source)
    {
        if (source.Count == 0) return 0.0; // Evita divisao por zero
        double avg = source.Average();
        return Math.Sqrt(source.Average(v => Math.Pow(v - avg, 2)));
    }
}

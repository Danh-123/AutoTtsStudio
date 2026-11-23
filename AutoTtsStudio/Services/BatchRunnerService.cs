using AutoTtsStudio.Models;
using AutoTtsStudio.Providers;
using NAudio.Wave;

namespace AutoTtsStudio.Services;

/// <summary>
/// Multi-threaded batch runner for synthesizing TTS items.
/// Supports smart resume, auto-retry with exponential backoff, proxy rotation,
/// NAudio duration calculation, and real-time thread-safe progress reporting.
/// </summary>
public sealed class BatchRunnerService
{
    private CancellationTokenSource? _cts;
    private readonly object _stateLock = new();
    private bool _isRunning = false;

    public bool IsRunning
    {
        get { lock (_stateLock) return _isRunning; }
        private set { lock (_stateLock) _isRunning = value; }
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    public async Task RunBatchAsync(
        IList<TtsLineItem> items,
        ITtsProvider provider,
        VoiceSettings settings,
        ProxyService? proxyService,
        bool useProxy,
        string outputFolder,
        int maxConcurrency,
        bool autoRetry,
        IProgress<TtsLineItem>? itemProgress,
        IProgress<(int Done, int Total)>? overallProgress,
        CancellationToken externalCt)
    {
        lock (_stateLock)
        {
            if (_isRunning) throw new InvalidOperationException("Batch job is already running.");
            _isRunning = true;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        var ct = _cts.Token;

        try
        {
            Directory.CreateDirectory(outputFolder);

            // Smart Resume: skip already completed items with existing files
            var itemsToProcess = items.Where(i =>
                !(i.Status == TtsStatus.Done &&
                  !string.IsNullOrEmpty(i.OutputPath) &&
                  File.Exists(i.OutputPath))).ToList();

            int totalCount = items.Count;
            int doneCount = items.Count(i => i.Status == TtsStatus.Done && File.Exists(i.OutputPath));
            overallProgress?.Report((doneCount, totalCount));

            if (itemsToProcess.Count == 0)
            {
                return;
            }

            using var semaphore = new SemaphoreSlim(Math.Max(1, maxConcurrency));
            var tasks = new List<Task>();

            foreach (var item in itemsToProcess)
            {
                if (ct.IsCancellationRequested) break;
                await semaphore.WaitAsync(ct);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        if (ct.IsCancellationRequested) return;

                        item.Status = TtsStatus.Processing;
                        item.ErrorMessage = string.Empty;
                        itemProgress?.Report(item);

                        var baseOutput = Path.Combine(outputFolder, $"line_{item.Index:D4}");
                        await ProcessSingleLineAsync(item, provider, settings, proxyService, useProxy, baseOutput, autoRetry, ct);

                        Interlocked.Increment(ref doneCount);
                        overallProgress?.Report((doneCount, totalCount));
                        itemProgress?.Report(item);
                    }
                    catch (OperationCanceledException)
                    {
                        if (item.Status == TtsStatus.Processing)
                        {
                            item.Status = TtsStatus.Pending;
                            itemProgress?.Report(item);
                        }
                    }
                    catch (Exception ex)
                    {
                        item.Status = TtsStatus.Error;
                        item.ErrorMessage = ex.Message;
                        itemProgress?.Report(item);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsRunning = false;
        }
    }

    public async Task RunSingleItemAsync(
        TtsLineItem item,
        ITtsProvider provider,
        VoiceSettings settings,
        ProxyService? proxyService,
        bool useProxy,
        string outputFolder,
        bool autoRetry,
        IProgress<TtsLineItem>? itemProgress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(outputFolder);
        item.Status = TtsStatus.Processing;
        item.ErrorMessage = string.Empty;
        itemProgress?.Report(item);

        try
        {
            var baseOutput = Path.Combine(outputFolder, $"line_{item.Index:D4}");
            await ProcessSingleLineAsync(item, provider, settings, proxyService, useProxy, baseOutput, autoRetry, ct);
        }
        catch (Exception ex)
        {
            item.Status = TtsStatus.Error;
            item.ErrorMessage = ex.Message;
        }
        finally
        {
            itemProgress?.Report(item);
        }
    }

    private static async Task ProcessSingleLineAsync(
        TtsLineItem item,
        ITtsProvider provider,
        VoiceSettings settings,
        ProxyService? proxyService,
        bool useProxy,
        string baseOutput,
        bool autoRetry,
        CancellationToken ct)
    {
        int maxAttempts = autoRetry ? 4 : 1; // 1 initial + 3 retries
        int[] delays = { 1000, 2000, 4000 };
        Exception? lastEx = null;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (ct.IsCancellationRequested) break;

            ProxyItem? proxy = null;
            if (useProxy && proxyService != null)
            {
                proxy = proxyService.GetNextProxy();
            }

            try
            {
                var finalPath = await provider.SynthesizeToFileAsync(item.Text, settings, baseOutput, proxy, ct);
                item.OutputPath = finalPath;

                // Read duration using NAudio
                if (File.Exists(finalPath))
                {
                    try
                    {
                        using var reader = new AudioFileReader(finalPath);
                        item.DurationSeconds = reader.TotalTime.TotalSeconds;
                    }
                    catch { }
                }

                item.Status = TtsStatus.Done;
                item.ErrorMessage = string.Empty;

                if (proxy != null && proxyService != null)
                {
                    proxyService.ReportResult(proxy, true);
                }

                return; // Success
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastEx = ex;

                if (proxy != null && proxyService != null)
                {
                    proxyService.ReportResult(proxy, false);
                }

                if (attempt < maxAttempts - 1)
                {
                    int delay = delays[Math.Min(attempt, delays.Length - 1)];
                    await Task.Delay(delay, ct);
                }
            }
        }

        item.Status = TtsStatus.Error;
        item.ErrorMessage = lastEx?.Message ?? "Lỗi không xác định";
        throw lastEx ?? new InvalidOperationException(item.ErrorMessage);
    }
}

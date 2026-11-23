using AutoTtsStudio.Models;

namespace AutoTtsStudio.Services;

/// <summary>
/// Thread-safe round-robin proxy rotation service.
/// Marks proxy dead after 3 consecutive failures.
/// </summary>
public sealed class ProxyService
{
    private readonly object _lock = new();
    private readonly List<ProxyItem> _proxies = new();
    private int _currentIndex = -1;

    public IReadOnlyList<ProxyItem> Proxies
    {
        get
        {
            lock (_lock) return _proxies.ToList();
        }
    }

    public void LoadFromString(string text)
    {
        lock (_lock)
        {
            _proxies.Clear();
            _currentIndex = -1;
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var item = ProxyItem.TryParse(line);
                if (item != null)
                {
                    item.Status = ProxyStatus.Live;
                    item.FailCount = 0;
                    _proxies.Add(item);
                }
            }
        }
    }

    public void LoadFromFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            var text = File.ReadAllText(filePath);
            LoadFromString(text);
        }
    }

    public ProxyItem? GetNextProxy()
    {
        lock (_lock)
        {
            var active = _proxies.Where(p => p.Status != ProxyStatus.Dead).ToList();
            if (active.Count == 0) return null;

            _currentIndex = (_currentIndex + 1) % active.Count;
            return active[_currentIndex];
        }
    }

    public void ReportResult(ProxyItem proxy, bool success)
    {
        lock (_lock)
        {
            if (success)
            {
                proxy.FailCount = 0;
                proxy.Status = ProxyStatus.Live;
            }
            else
            {
                proxy.FailCount++;
                if (proxy.FailCount >= 3)
                {
                    proxy.Status = ProxyStatus.Dead;
                }
            }
        }
    }

    public (int Live, int Die, int Total) GetCounts()
    {
        lock (_lock)
        {
            int live = _proxies.Count(p => p.Status != ProxyStatus.Dead);
            int die = _proxies.Count(p => p.Status == ProxyStatus.Dead);
            int total = _proxies.Count;
            return (live, die, total);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _proxies.Clear();
            _currentIndex = -1;
        }
    }
}

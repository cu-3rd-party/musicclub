namespace CuMusicClub.Application.Services.Auth;

public class SimpleRateLimiter
{
    private readonly Dictionary<string, List<DateTimeOffset>> _requests = new();
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private readonly object _lock = new();

    public SimpleRateLimiter(int maxRequests, TimeSpan window)
    {
        _maxRequests = maxRequests;
        _window = window;
    }

    public bool IsAllowed(string key)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;

            if (!_requests.ContainsKey(key))
            {
                _requests[key] = [];
            }

            var requests = _requests[key];
            requests.RemoveAll(r => now - r > _window);

            if (requests.Count >= _maxRequests)
            {
                return false;
            }

            requests.Add(now);
            return true;
        }
    }
}

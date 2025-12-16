using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace osafw.Tests;

internal static class TestHelpers
{
    public static FW CreateFw(IDictionary<string, string?>? settings = null)
    {
        var context = new DefaultHttpContext
        {
            Session = new FakeSession(),
        };

        var configurationBuilder = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string?>());
        var fw = new FW(context, configurationBuilder.Build());
        return fw;
    }

    public static void RegisterModel<T>(FW fw, T model) where T : class
    {
        var modelsField = typeof(FW).GetField("models", BindingFlags.NonPublic | BindingFlags.Instance);
        if (modelsField == null)
            throw new System.InvalidOperationException("models field not found on FW");

        if (modelsField.GetValue(fw) is not FwDict cache)
            throw new System.InvalidOperationException("models cache is not initialized");

        cache[typeof(T).Name] = model;
    }

    public class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> store = new();

        public IEnumerable<string> Keys => store.Keys;
        public string Id { get; } = System.Guid.NewGuid().ToString();
        public bool IsAvailable => true;

        public void Clear() => store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => store.Remove(key);
        public void Set(string key, byte[] value) => store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => store.TryGetValue(key, out value!);
    }
}

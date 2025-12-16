using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace osafw.Tests
{
    [TestClass]
    public class FwTests
    {
        [TestMethod]
        public void FormatUserDateTime_FormatsIsoAndLocal()
        {
            var context = new DefaultHttpContext
            {
                Session = new FakeSession(),
            };
            var configuration = new ConfigurationBuilder().Build();

            var fw = new FW(context, configuration);

            var dt = new System.DateTime(2024, 1, 1, 12, 0, 0, System.DateTimeKind.Utc);

            var iso = fw.formatUserDateTime(dt, true);
            var local = fw.formatUserDateTime(dt);

            Assert.AreEqual("2024-01-01T12:00:00+00:00", iso);
            Assert.AreEqual("1/1/2024 12:00 PM", local);
        }

        [TestMethod]
        public void FormatUserDateTime_HonorsUserFormatsAndSqlInput()
        {
            var context = new DefaultHttpContext
            {
                Session = new FakeSession(),
            };
            var configuration = new ConfigurationBuilder().Build();

            var fw = new FW(context, configuration);
            fw.G["date_format"] = DateUtils.DATE_FORMAT_DMY;
            fw.G["time_format"] = DateUtils.TIME_FORMAT_24;

            var formatted = fw.formatUserDateTime("2024-02-03 15:30:00");

            Assert.AreEqual("3/2/2024 15:30", formatted);
        }

        // Minimal ISession for FW unit testing
        private class FakeSession : ISession
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
}

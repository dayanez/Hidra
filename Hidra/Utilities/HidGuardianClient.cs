using System;
using System.Diagnostics;
using System.Net.Http;

namespace Hidra.Utilities
{
    internal class HidGuardianClient : IDisposable
    {
        private const string HidGuardianUrl = "http://localhost:26762/api/v1/hidguardian";
        private readonly HttpClient _client;

        public HidGuardianClient()
        {
            _client = new HttpClient();
        }

        public void WhitelistProcess()
        {
            TryGet($"{HidGuardianUrl}/whitelist/add/{Process.GetCurrentProcess().Id}");
        }

        public void RemoveWhitelistProcess()
        {
            TryGet($"{HidGuardianUrl}/whitelist/remove/{Process.GetCurrentProcess().Id}");
        }

        private void TryGet(string url)
        {
            try
            {
                _client.GetAsync(url).GetAwaiter().GetResult();
            }
            catch (HttpRequestException)
            {
                // HidGuardian service not present/running - not fatal, mirrors prior RestSharp behavior
            }
        }

        public void Dispose()
        {
            RemoveWhitelistProcess();
            _client.Dispose();
        }
    }
}

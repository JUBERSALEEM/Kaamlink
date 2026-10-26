using Microsoft.Maui.Networking;

namespace KaamLink.Core.Api
{
    /// <summary>Testable wrapper around MAUI Connectivity for the transport layer.</summary>
    public interface INetworkStatus
    {
        bool HasUsableNetwork();

        string Describe();
    }

    public sealed class NetworkStatus : INetworkStatus
    {
        public bool HasUsableNetwork()
        {
            try
            {
                // Local/ConstrainedInternet still allows a reachable dev or LAN API.
                // Only None/Unknown means there is no point opening a socket.
                return Connectivity.Current.NetworkAccess is
                    NetworkAccess.Internet or NetworkAccess.ConstrainedInternet or NetworkAccess.Local;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KaamLink] connectivity probe failed: {exception}");
                // If Essentials is unavailable (unit-test host), let the request try
                // and let HttpClient's timeout produce the real transport error.
                return true;
            }
        }

        public string Describe()
        {
            try
            {
                return Connectivity.Current.NetworkAccess.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}

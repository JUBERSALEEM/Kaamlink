namespace KaamLink.Core.Api
{
    /// <summary>Stores the token pair in the platform secure enclave (Android Keystore via SecureStorage).</summary>
    public interface ITokenStore
    {
        Task SaveAsync(string accessToken, string refreshToken);

        Task<string?> GetAccessTokenAsync();

        Task<string?> GetRefreshTokenAsync();

        Task ClearAsync();
    }

    /// <summary>
    /// SecureStorage-backed store. Tokens never touch files/preferences and are
    /// excluded from backups (allowBackup=false in the manifest).
    /// </summary>
    public sealed class SecureStorageTokenStore : ITokenStore
    {
        public static class Keys
        {
            public const string AccessToken = "auth.access";
            public const string RefreshToken = "auth.refresh";
        }

        public async Task SaveAsync(string accessToken, string refreshToken)
        {
            await SecureStorage.Default.SetAsync(Keys.AccessToken, accessToken);
            await SecureStorage.Default.SetAsync(Keys.RefreshToken, refreshToken);
        }

        public Task<string?> GetAccessTokenAsync() => GetOrNullAsync(Keys.AccessToken);

        public Task<string?> GetRefreshTokenAsync() => GetOrNullAsync(Keys.RefreshToken);

        public Task ClearAsync()
        {
            SecureStorage.Default.Remove(Keys.AccessToken);
            SecureStorage.Default.Remove(Keys.RefreshToken);
            return Task.CompletedTask;
        }

        private static async Task<string?> GetOrNullAsync(string key)
        {
            var value = await SecureStorage.Default.GetAsync(key);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}

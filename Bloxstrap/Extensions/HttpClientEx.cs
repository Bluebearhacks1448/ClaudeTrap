namespace Bloxstrap.Extensions
{
    internal static class HttpClientEx
    {
        private static void ValidateRetryArguments(HttpClient client, string url, int retries)
        {
            if (client is null)
                throw new ArgumentNullException(nameof(client));

            if (String.IsNullOrWhiteSpace(url))
                throw new ArgumentException("A request URL must be provided.", nameof(url));

            if (retries < 1)
                throw new ArgumentOutOfRangeException(nameof(retries), "At least one request attempt is required.");
        }

        public static async Task<HttpResponseMessage> GetWithRetriesAsync(this HttpClient client, string url, int retries, CancellationToken token)
        {
            ValidateRetryArguments(client, url, retries);

            for (int i = 1; i <= retries; i++)
            {
                try
                {
                    return await client.GetAsync(url, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // A caller-requested cancellation must never be retried.
                    throw;
                }
                catch (OperationCanceledException ex)
                {
                    App.Logger.WriteException("HttpClientEx::GetWithRetriesAsync", ex);
                    if (i == retries)
                        throw;
                }
                catch (HttpRequestException ex)
                {
                    App.Logger.WriteException("HttpClientEx::GetWithRetriesAsync", ex);
                    if (i == retries)
                        throw;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(250 * i, 1000)), token).ConfigureAwait(false);
            }

            throw new InvalidOperationException("The HTTP request ended without a response.");
        }

        public static async Task<HttpResponseMessage> PostWithRetriesAsync(this HttpClient client, string url, HttpContent? content, int retries, CancellationToken token)
        {
            ValidateRetryArguments(client, url, retries);

            for (int i = 1; i <= retries; i++)
            {
                try
                {
                    return await client.PostAsync(url, content, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // A caller-requested cancellation must never be retried.
                    throw;
                }
                catch (OperationCanceledException ex)
                {
                    App.Logger.WriteException("HttpClientEx::PostWithRetriesAsync", ex);
                    if (i == retries)
                        throw;
                }
                catch (HttpRequestException ex)
                {
                    App.Logger.WriteException("HttpClientEx::PostWithRetriesAsync", ex);
                    if (i == retries)
                        throw;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(250 * i, 1000)), token).ConfigureAwait(false);
            }

            throw new InvalidOperationException("The HTTP request ended without a response.");
        }

        public static async Task<T?> GetFromJsonWithRetriesAsync<T>(this HttpClient client, string url, int retries, CancellationToken token) where T : class
        {
            using HttpResponseMessage response = await GetWithRetriesAsync(client, url, retries, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: token).ConfigureAwait(false);
        }

        public static async Task<T?> PostFromJsonWithRetriesAsync<T>(this HttpClient client, string url, HttpContent? content, int retries, CancellationToken token) where T : class
        {
            using HttpResponseMessage response = await PostWithRetriesAsync(client, url, content, retries, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: token).ConfigureAwait(false);
        }
    }
}

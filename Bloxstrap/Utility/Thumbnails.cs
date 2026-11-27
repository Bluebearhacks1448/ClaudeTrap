using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bloxstrap.Utility
{
    internal static class Thumbnails
    {
        // TODO: remove requests from list once they're finished or failed
        /// <remarks>
        /// Returned array may contain null values
        /// </remarks>
        public static async Task<string?[]> GetThumbnailUrlsAsync(List<ThumbnailRequest> requests, CancellationToken token)
        {
            const string LOG_IDENT = "Thumbnails::GetThumbnailUrlsAsync";
            const int RETRIES = 5;
            const int RETRY_TIME_INCREMENT = 500; // ms

            if (requests is null)
                throw new ArgumentNullException(nameof(requests));

            if (requests.Count == 0)
                return Array.Empty<string?>();

            string?[] urls = new string?[requests.Count];

            // assign unique request ids to each request
            for (int i = 0; i < requests.Count; i++)
                requests[i].RequestId = i.ToString();

            var payload = new StringContent(JsonSerializer.Serialize(requests), Encoding.UTF8, "application/json");

            ThumbnailResponse[] response = null!;

            for (int i = 1; i <= RETRIES; i++)
            {
                var json = await App.HttpClient.PostFromJsonWithRetriesAsync<ThumbnailBatchResponse>("https://thumbnails.roblox.com/v1/batch", payload, 3, token);
                if (json == null)
                    throw new InvalidHTTPResponseException("Deserialised ThumbnailBatchResponse is null");

                response = json.Data ?? Array.Empty<ThumbnailResponse>();
                if (response.Length == 0)
                    throw new InvalidHTTPResponseException("ThumbnailBatchResponse contained no thumbnail data.");

                bool finished = response.All(x => !String.Equals(x.State, "Pending", StringComparison.OrdinalIgnoreCase));
                if (finished)
                    break;

                if (i == RETRIES)
                    App.Logger.WriteLine(LOG_IDENT, "Ran out of retries");
                else
                    await Task.Delay(RETRY_TIME_INCREMENT * i, token);
            }

            foreach (var item in response)
            {
                if (item is null || !int.TryParse(item.RequestId, out int requestIndex)
                    || requestIndex < 0 || requestIndex >= urls.Length)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Ignoring thumbnail response with an invalid requestId.");
                    continue;
                }
                if (String.Equals(item.State, "Pending", StringComparison.OrdinalIgnoreCase))
                    App.Logger.WriteLine(LOG_IDENT, $"{item.TargetId} is still pending");
                else if (String.Equals(item.State, "Error", StringComparison.OrdinalIgnoreCase))
                    App.Logger.WriteLine(LOG_IDENT, $"{item.TargetId} got error code {item.ErrorCode} ({item.ErrorMessage})");
                else if (!String.Equals(item.State, "Completed", StringComparison.OrdinalIgnoreCase))
                    App.Logger.WriteLine(LOG_IDENT, $"{item.TargetId} got \"{item.State}\"");

                urls[requestIndex] = item.ImageUrl;
            }

            return urls;
        }

        public static async Task<string?> GetThumbnailUrlAsync(ThumbnailRequest request, CancellationToken token)
        {
            const string LOG_IDENT = "Thumbnails::GetThumbnailUrlAsync";
            const int RETRIES = 5;
            const int RETRY_TIME_INCREMENT = 500; // ms

            if (request is null)
                throw new ArgumentNullException(nameof(request));

            request.RequestId = "0";

            var payload = new StringContent(JsonSerializer.Serialize(new ThumbnailRequest[] { request }), Encoding.UTF8, "application/json");

            ThumbnailResponse response = null!;

            for (int i = 1; i <= RETRIES; i++)
            {
                var json = await App.HttpClient.PostFromJsonWithRetriesAsync<ThumbnailBatchResponse>("https://thumbnails.roblox.com/v1/batch", payload, 3, token);
                if (json == null)
                    throw new InvalidHTTPResponseException("Deserialised ThumbnailBatchResponse is null");

                if (json.Data is null || json.Data.Length == 0 || json.Data[0] is null)
                    throw new InvalidHTTPResponseException("ThumbnailBatchResponse contained no thumbnail data.");

                response = json.Data[0];

                if (!String.Equals(response.State, "Pending", StringComparison.OrdinalIgnoreCase))
                    break;

                if (i == RETRIES)
                    App.Logger.WriteLine(LOG_IDENT, "Ran out of retries");
                else
                    await Task.Delay(RETRY_TIME_INCREMENT * i, token);
            }

            if (String.Equals(response.State, "Pending", StringComparison.OrdinalIgnoreCase))
                App.Logger.WriteLine(LOG_IDENT, $"{response.TargetId} is still pending");
            else if (String.Equals(response.State, "Error", StringComparison.OrdinalIgnoreCase))
                App.Logger.WriteLine(LOG_IDENT, $"{response.TargetId} got error code {response.ErrorCode} ({response.ErrorMessage})");
            else if (!String.Equals(response.State, "Completed", StringComparison.OrdinalIgnoreCase))
                App.Logger.WriteLine(LOG_IDENT, $"{response.TargetId} got \"{response.State}\"");

            return response.ImageUrl;
        }
    }
}

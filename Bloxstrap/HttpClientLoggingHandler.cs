namespace Bloxstrap
{
    internal class HttpClientLoggingHandler : MessageProcessingHandler
    {
        public HttpClientLoggingHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        private static string GetSafeRequestUri(HttpRequestMessage? request)
        {
            Uri? uri = request?.RequestUri;
            if (uri is null)
                return "<unknown>";

            // Query parameters can contain launch tickets or other short-lived credentials.
            // Keep the endpoint path for diagnostics, but never persist query or fragment data.
            return uri.IsAbsoluteUri
                ? uri.GetLeftPart(UriPartial.Path)
                : uri.OriginalString.Split('?', '#')[0];
        }

        protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            App.Logger.WriteLine(
                "HttpClientLoggingHandler::ProcessRequest",
                $"{request.Method} {GetSafeRequestUri(request)}"
            );
            return request;
        }

        protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            App.Logger.WriteLine(
                "HttpClientLoggingHandler::ProcessResponse",
                $"{(int)response.StatusCode} {response.ReasonPhrase} {GetSafeRequestUri(response.RequestMessage)}"
            );
            return response;
        }
    }
}

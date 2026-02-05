using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Z9Flex
{
    /// <summary>
    /// Encapsulates a specialized HttpClient that automatically handles authentication to the Z9Flex application.
    /// </summary>
    public class Z9HttpClient : HttpClient
    {
        private readonly Z9AuthenticationProvider _authenticationProvider;

        /// <inheritdoc />
        public Z9HttpClient(Z9AuthenticationProvider authenticationProvider)
        {
            _authenticationProvider = authenticationProvider;
        }

        /// <inheritdoc />       
        public Z9HttpClient(HttpMessageHandler handler, Z9AuthenticationProvider authenticationProvider) : base(handler)
        {
            _authenticationProvider = authenticationProvider;
        }
        
        /// <inheritdoc />
        public Z9HttpClient(HttpMessageHandler handler, bool disposeHandler, Z9AuthenticationProvider authenticationProvider) : base(handler, disposeHandler)
        {
            _authenticationProvider = authenticationProvider;
        }

        /// <inheritdoc />
        public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

            await _authenticationProvider.RefreshTokenAsync();

            var cloneRequest = CloneRequest(request);

            cloneRequest.Headers.Remove("sessionToken");
            cloneRequest.Headers.Add("sessionToken", _authenticationProvider.CurrentAuthenticationResult.SessionToken);

            return await base.SendAsync(cloneRequest, cancellationToken); // Retry the request
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = request.Content
            };

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}
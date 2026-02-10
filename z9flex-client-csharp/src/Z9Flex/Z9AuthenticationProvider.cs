using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Z9Flex.Client;
using Z9Flex.Client.Models;

namespace Z9Flex
{
    /// <summary>
    /// Represents an authentication provider for the Z9Flex application.
    /// </summary>
    public class Z9AuthenticationProvider : IAuthenticationProvider
    {
        private readonly FlexClient _authenticateClient;
        private readonly CredentialsCallback _credentialsCallback;
        private DateTimeOffset _expiration;

        private Z9AuthenticationProvider(FlexClient authenticateClient, CredentialsCallback credentialsCallback)
        {
            _authenticateClient = authenticateClient;
            _credentialsCallback = credentialsCallback;
        }

        /// <summary>
        /// Creates a new instance of Z9AuthenticationProvider.
        /// </summary>
        /// <param name="baseUrl">The base URL of the Z9Flex application.</param>
        /// <param name="credentialsCallback">A callback function that provides the credentials for authentication.</param>
        /// <param name="httpClientHandler">An optional HttpClientHandler to be used for HTTP requests.</param>
        /// <returns>A new instance of Z9AuthenticationProvider.</returns>
        public static Z9AuthenticationProvider CreateInstance(string baseUrl, CredentialsCallback credentialsCallback,
            WinHttpHandler httpClientHandler = null)
        {
            if (httpClientHandler == null) httpClientHandler = new WinHttpHandler();

            return new Z9AuthenticationProvider(
                new FlexClient(new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(),
                        httpClient: new HttpClient(httpClientHandler))
                    { BaseUrl = baseUrl }), credentialsCallback);
        }

        /// <summary>
        /// Creates a new instance of Z9AuthenticationProvider with a cross-platform HttpMessageHandler.
        /// </summary>
        /// <param name="baseUrl">The base URL of the Z9Flex application.</param>
        /// <param name="credentialsCallback">A callback function that provides the credentials for authentication.</param>
        /// <param name="httpMessageHandler">An HttpMessageHandler to be used for HTTP requests.</param>
        /// <returns>A new instance of Z9AuthenticationProvider.</returns>
        public static Z9AuthenticationProvider CreateInstance(string baseUrl, CredentialsCallback credentialsCallback,
            HttpMessageHandler httpMessageHandler)
        {
            return new Z9AuthenticationProvider(
                new FlexClient(new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(),
                        httpClient: new HttpClient(httpMessageHandler))
                    { BaseUrl = baseUrl }), credentialsCallback);
        }

        /// <inheritdoc />
        public async Task AuthenticateRequestAsync(RequestInformation request,
            Dictionary<string, object> additionalAuthenticationContext = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            if (string.IsNullOrWhiteSpace(CurrentAuthenticationResult?.SessionToken) ||
                DateTimeOffset.UtcNow >= _expiration)
            {
                await RefreshTokenAsync();
            }

            if (CurrentAuthenticationResult?.SessionToken != null)
                request.Headers.Add("sessionToken", CurrentAuthenticationResult.SessionToken);
        }

        /// <summary>
        /// Refreshes the authentication token.
        /// </summary>
        public async Task RefreshTokenAsync()
        {
            var (username, password) = _credentialsCallback();

            var result = await _authenticateClient.Authenticate.PostAsync(new AuthenticateRequest
                { Username = username, Password = password, ApiClientType = Enums.ApiClientType.Api });
            CurrentAuthenticationResult = result;
            _expiration = DateTimeOffset.UtcNow.AddMinutes(30);
        }

        /// <summary>
        /// Represents an authentication provider for the Z9Flex application.
        /// </summary>
        public delegate (string username, string password) CredentialsCallback();
        
        /// <summary>
        /// Represents the current authentication result for the Z9Flex application.
        /// </summary>
        public AuthenticateResult CurrentAuthenticationResult { get; private set; }
    }
}
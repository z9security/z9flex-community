# Z9/Flex Community Profile

This repository contains the OpenAPI specification for the Z9/Flex JSON/REST API - Community Profile.

## Overview

The Community Profile is a profile of Z9 Security's Z9/Flex API, designed to be the interface and data model for an open, interoperable access control platform.

## See Also

- [Z9/Open Community Profile](https://github.com/z9security/z9open-community) - Google Protobuf specification for the Z9/Open binary protocol Community Profile

## Z9/Flex and Z9/Open

For more information about the commercial version of Z9/Flex, visit [z9flex.com](https://z9flex.com).

For more information about the commercial version of Z9/Open, visit [z9open.com](https://z9open.com).

Z9/FL=X is a registered trademark of Z9 Security. z9/op=n is a registered certification mark of Z9 Security.

## License

Apache 2.0 - see [LICENSE](LICENSE) for details.

## Files

- `z9flex-swagger-community.yaml` - OpenAPI 3.0 specification
- `z9flex-client-csharp/` - C#/.NET client library

## Usage

The swagger file can be used with OpenAPI tools to generate client libraries in various languages.

## C# Client Library

The `z9flex-client-csharp/` directory contains a C#/.NET client library (`Z9.Flex.Community`) for the Z9/Flex Community Profile API. It is built using [Microsoft Kiota](https://learn.microsoft.com/en-us/openapi/kiota/) to auto-generate a strongly-typed REST client from the OpenAPI specification.

The library targets .NET Standard 2.0, making it compatible with .NET Framework 4.7+, .NET Core 2.0+, and .NET 6+.

### Getting Started

Create an authentication provider and adapter, then use the client to make API calls:

```csharp
var handler = new WinHttpHandler
{
    ServerCertificateValidationCallback = (message, certificate2, arg3, arg4) => true
};

var authenticationProvider = Z9AuthenticationProvider.CreateInstance(baseUrl,
    () => (username: z9Username, password: z9Password), handler);

var adapter = new HttpClientRequestAdapter(authenticationProvider,
        httpClient: new Z9HttpClient(handler, authenticationProvider))
    { BaseUrl = baseUrl };

var flexClient = new Z9Flex.Client.FlexClient(adapter);
```

Then use the client to interact with the API:

```csharp
var devices = await flexClient.Dev.List.GetAsync();
var schedules = await flexClient.Sched.List.GetAsync();
```

### Regenerating the Client

The auto-generated client code can be regenerated from the swagger using [Microsoft Kiota](https://learn.microsoft.com/en-us/openapi/kiota/install):

```bash
dotnet tool install --global Microsoft.OpenApi.Kiota

kiota generate -l CSharp -c FlexClient -n Z9Flex.Client \
  -d z9flex-swagger-community.yaml \
  -o z9flex-client-csharp/src/Z9Flex/Client
```

After generation, apply the following post-processing fix to the generated models. Kiota generates unqualified `Time?` references that conflict with `System.Time`, so they must be fully qualified:

In `z9flex-client-csharp/src/Z9Flex/Client/Models/SchedElement.cs`, replace:
```csharp
public Time? Start { get; set; }
public Time? Stop { get; set; }
```
with:
```csharp
public Microsoft.Kiota.Abstractions.Time? Start { get; set; }
public Microsoft.Kiota.Abstractions.Time? Stop { get; set; }
```

### Publishing to NuGet

Pushing a version tag triggers the CI pipeline to build and publish the `Z9.Flex.Community` package to nuget.org:

Find the latest version and tag the next:

```bash
git tag --sort=-v:refname | head -1
```

```bash
git tag v1.0.1
git push origin v1.0.1
```

Note: This requires a `NUGET_API_KEY` secret configured in the repository's GitHub Actions settings.

Verify the package is published:

```bash
curl -s https://api.nuget.org/v3-flatcontainer/z9.flex.community/index.json
```

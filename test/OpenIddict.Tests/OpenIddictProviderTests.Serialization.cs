/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AspNet.Security.OpenIdConnect.Client;
using AspNet.Security.OpenIdConnect.Extensions;
using AspNet.Security.OpenIdConnect.Primitives;
using AspNet.Security.OpenIdConnect.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenIddict.Core;
using OpenIddict.Models;
using Xunit;

namespace OpenIddict.Tests
{
    public partial class OpenIddictProviderTests
    {
        [Fact]
        public async Task DeserializeAccessToken_ReturnsNullForMalformedReferenceToken()
        {
            // Arrange
            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();
            var manager = CreateTokenManager();

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Introspection, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();

                builder.Configure(options => options.AccessTokenFormat = format.Object);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Token = "2YotnFZFEjr1zCsicMWpAA",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.AccessToken
            });

            // Assert
            Assert.Single(response.GetParameters());
            Assert.False((bool) response[OpenIdConnectConstants.Claims.Active]);

            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Never());
            Mock.Get(manager).Verify(mock => mock.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task DeserializeAccessToken_AccessTokenIsNotRetrievedFromDatabaseWhenReferenceTokensAreDisabled()
        {
            // Arrange
            var identity = new ClaimsIdentity(OpenIdConnectServerDefaults.AuthenticationScheme);
            identity.AddClaim(OpenIdConnectConstants.Claims.Subject, "Bob le Bricoleur");

            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                new AuthenticationProperties(),
                OpenIdConnectServerDefaults.AuthenticationScheme);

            ticket.SetAudiences("Fabrikam");
            ticket.SetTokenId("3E228451-1555-46F7-A471-951EFBA23A56");
            ticket.SetTokenUsage(OpenIdConnectConstants.TokenUsages.AccessToken);

            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(ticket);

            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.IsValidAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Introspection, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Services.AddSingleton(manager);

                builder.Configure(options => options.AccessTokenFormat = format.Object);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Token = "2YotnFZFEjr1zCsicMWpAA",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.AccessToken
            });

            // Assert
            Assert.True((bool) response[OpenIdConnectConstants.Claims.Active]);

            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Once());
            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task DeserializeAccessToken_ReturnsNullForMissingReferenceTokenIdentifier()
        {
            // Arrange
            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>(result: null));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Introspection, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Token = "HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.AccessToken
            });

            // Assert
            Assert.Single(response.GetParameters());
            Assert.False((bool) response[OpenIdConnectConstants.Claims.Active]);

            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()), Times.Once());
            Mock.Get(manager).Verify(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        }

        [Fact]
        public async Task DeserializeAccessToken_ReturnsNullForMissingReferenceTokenCiphertext()
        {
            // Arrange
            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.GetPayloadAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>(result: null));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Introspection, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Token = "HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.AccessToken
            });

            // Assert
            Assert.Single(response.GetParameters());
            Assert.False((bool) response[OpenIdConnectConstants.Claims.Active]);

            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()), Times.Once());
            Mock.Get(manager).Verify(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        }

        [Fact]
        public async Task DeserializeAccessToken_ReturnsNullForInvalidReferenceTokenCiphertext()
        {
            // Arrange
            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(value: null);

            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.GetPayloadAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("2YotnFZFEjr1zCsicMWpAA"));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Introspection, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();

                builder.Configure(options => options.AccessTokenFormat = format.Object);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Token = "HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.AccessToken
            });

            // Assert
            Assert.Single(response.GetParameters());
            Assert.False((bool) response[OpenIdConnectConstants.Claims.Active]);

            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()), Times.Once());
            Mock.Get(manager).Verify(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Once());
        }

        [Fact]
        public async Task DeserializeAccessToken_ReturnsExpectedReferenceToken()
        {
            // Arrange
            var identity = new ClaimsIdentity(OpenIdConnectServerDefaults.AuthenticationScheme);
            identity.AddClaim(OpenIdConnectConstants.Claims.Subject, "Bob le Bricoleur");

            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                new AuthenticationProperties(),
                OpenIdConnectServerDefaults.AuthenticationScheme);

            ticket.SetAudiences("Fabrikam");
            ticket.SetTokenId("3E228451-1555-46F7-A471-951EFBA23A56");

            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(ticket);

            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.GetPayloadAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("2YotnFZFEjr1zCsicMWpAA"));

                instance.Setup(mock => mock.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.IsValidAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                instance.Setup(mock => mock.GetCreationDateAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<DateTimeOffset?>(new DateTimeOffset(2017, 01, 01, 00, 00, 00, TimeSpan.Zero)));

                instance.Setup(mock => mock.GetExpirationDateAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<DateTimeOffset?>(new DateTimeOffset(2017, 01, 10, 00, 00, 00, TimeSpan.Zero)));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Introspection, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();

                builder.Configure(options =>
                {
                    options.SystemClock = Mock.Of<ISystemClock>(mock => mock.UtcNow ==
                        new DateTimeOffset(2017, 01, 05, 00, 00, 00, TimeSpan.Zero));

                    options.AccessTokenFormat = format.Object;
                });
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(IntrospectionEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Token = "HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ",
                TokenTypeHint = OpenIdConnectConstants.TokenTypeHints.AccessToken
            });

            // Assert
            Assert.True((bool) response[OpenIdConnectConstants.Claims.Active]);
            Assert.Equal("3E228451-1555-46F7-A471-951EFBA23A56", response[OpenIdConnectConstants.Claims.JwtId]);
            Assert.Equal(1483228800, (long) response[OpenIdConnectConstants.Claims.IssuedAt]);
            Assert.Equal(1484006400, (long) response[OpenIdConnectConstants.Claims.ExpiresAt]);

            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()), Times.Once());
            Mock.Get(manager).Verify(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()), Times.Once());
            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Once());
        }

        [Fact]
        public async Task DeserializeAuthorizationCode_ReturnsNullForMalformedReferenceToken()
        {
            // Arrange
            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();
            var manager = CreateTokenManager();

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Token, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();

                builder.Configure(options => options.AuthorizationCodeFormat = format.Object);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Code = "2YotnFZFEjr1zCsicMWpAA",
                GrantType = OpenIdConnectConstants.GrantTypes.AuthorizationCode,
                RedirectUri = "http://www.fabrikam.com/path"
            });

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.InvalidGrant, response.Error);
            Assert.Equal("The specified authorization code is invalid.", response.ErrorDescription);

            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Never());
            Mock.Get(manager).Verify(mock => mock.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task DeserializeAuthorizationCode_AuthorizationCodeIsNotRetrievedUsingHashWhenReferenceTokensAreDisabled()
        {
            // Arrange
            var identity = new ClaimsIdentity(OpenIdConnectServerDefaults.AuthenticationScheme);
            identity.AddClaim(OpenIdConnectConstants.Claims.Subject, "Bob le Bricoleur");

            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                new AuthenticationProperties(),
                OpenIdConnectServerDefaults.AuthenticationScheme);

            ticket.SetPresenters("Fabrikam");
            ticket.SetTokenId("3E228451-1555-46F7-A471-951EFBA23A56");

            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(ticket);

            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.IsValidAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Token, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Services.AddSingleton(manager);

                builder.Configure(options => options.AuthorizationCodeFormat = format.Object);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Code = "2YotnFZFEjr1zCsicMWpAA",
                GrantType = OpenIdConnectConstants.GrantTypes.AuthorizationCode,
                RedirectUri = "http://www.fabrikam.com/path"
            });

            // Assert
            Assert.NotNull(response.AccessToken);

            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Once());
            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task DeserializeAuthorizationCode_ReturnsNullForMissingReferenceTokenIdentifier()
        {
            // Arrange
            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>(result: null));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Token, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Code = "HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ",
                GrantType = OpenIdConnectConstants.GrantTypes.AuthorizationCode,
                RedirectUri = "http://www.fabrikam.com/path"
            });

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.InvalidGrant, response.Error);
            Assert.Equal("The specified authorization code is invalid.", response.ErrorDescription);

            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()), Times.Once());
            Mock.Get(manager).Verify(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        }

        [Fact]
        public async Task DeserializeAuthorizationCode_ReturnsNullForMissingReferenceTokenCiphertext()
        {
            // Arrange
            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.GetPayloadAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>(result: null));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Token, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Code = "HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ",
                GrantType = OpenIdConnectConstants.GrantTypes.AuthorizationCode,
                RedirectUri = "http://www.fabrikam.com/path"
            });

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.InvalidGrant, response.Error);
            Assert.Equal("The specified authorization code is invalid.", response.ErrorDescription);

            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()), Times.Once());
            Mock.Get(manager).Verify(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        }

        [Fact]
        public async Task DeserializeAuthorizationCode_ReturnsNullForInvalidReferenceTokenCiphertext()
        {
            // Arrange
            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(value: null);

            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.GetPayloadAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("2YotnFZFEjr1zCsicMWpAA"));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Token, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();

                builder.Configure(options => options.AuthorizationCodeFormat = format.Object);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Code = "HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ",
                GrantType = OpenIdConnectConstants.GrantTypes.AuthorizationCode,
                RedirectUri = "http://www.fabrikam.com/path"
            });

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.InvalidGrant, response.Error);
            Assert.Equal("The specified authorization code is invalid.", response.ErrorDescription);

            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()), Times.Once());
            Mock.Get(manager).Verify(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Once());
        }

        [Fact]
        public async Task DeserializeAuthorizationCode_ReturnsExpectedReferenceToken()
        {
            // Arrange
            var identity = new ClaimsIdentity(OpenIdConnectServerDefaults.AuthenticationScheme);
            identity.AddClaim(OpenIdConnectConstants.Claims.Subject, "Bob le Bricoleur");

            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                new AuthenticationProperties(),
                OpenIdConnectServerDefaults.AuthenticationScheme);

            ticket.SetPresenters("Fabrikam");

            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(ticket);

            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.GetPayloadAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("2YotnFZFEjr1zCsicMWpAA"));

                instance.Setup(mock => mock.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.IsValidAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                instance.Setup(mock => mock.GetCreationDateAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<DateTimeOffset?>(new DateTimeOffset(2017, 01, 01, 00, 00, 00, TimeSpan.Zero)));

                instance.Setup(mock => mock.GetExpirationDateAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<DateTimeOffset?>(new DateTimeOffset(2017, 01, 10, 00, 00, 00, TimeSpan.Zero)));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Token, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();

                builder.Configure(options =>
                {
                    options.SystemClock = Mock.Of<ISystemClock>(mock => mock.UtcNow ==
                        new DateTimeOffset(2017, 01, 05, 00, 00, 00, TimeSpan.Zero));

                    options.AuthorizationCodeFormat = format.Object;
                });
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Code = "HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ",
                GrantType = OpenIdConnectConstants.GrantTypes.AuthorizationCode,
                RedirectUri = "http://www.fabrikam.com/path"
            });

            // Assert
            Assert.NotNull(response.AccessToken);

            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()), Times.Once());
            Mock.Get(manager).Verify(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Once());
        }

        [Fact]
        public async Task DeserializeAuthorizationCode_ReturnsNullForMissingTokenIdentifier()
        {
            // Arrange
            var identity = new ClaimsIdentity(OpenIdConnectServerDefaults.AuthenticationScheme);
            identity.AddClaim(OpenIdConnectConstants.Claims.Subject, "Bob le Bricoleur");

            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                new AuthenticationProperties(),
                OpenIdConnectServerDefaults.AuthenticationScheme);

            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(ticket);

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Token, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Configure(options => options.AuthorizationCodeFormat = format.Object);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Code = "2YotnFZFEjr1zCsicMWpAA",
                GrantType = OpenIdConnectConstants.GrantTypes.AuthorizationCode,
                RedirectUri = "http://www.fabrikam.com/path"
            });

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.InvalidGrant, response.Error);
            Assert.Equal("The specified authorization code is invalid.", response.ErrorDescription);
        }

        [Fact]
        public async Task DeserializeAuthorizationCode_ReturnsNullForInvalidTokenCiphertext()
        {
            // Arrange
            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(value: null);

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Token, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Configure(options => options.AuthorizationCodeFormat = format.Object);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Code = "2YotnFZFEjr1zCsicMWpAA",
                GrantType = OpenIdConnectConstants.GrantTypes.AuthorizationCode,
                RedirectUri = "http://www.fabrikam.com/path"
            });

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.InvalidGrant, response.Error);
            Assert.Equal("The specified authorization code is invalid.", response.ErrorDescription);

            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Once());
        }

        [Fact]
        public async Task DeserializeAuthorizationCode_ReturnsExpectedToken()
        {
            // Arrange
            var identity = new ClaimsIdentity(OpenIdConnectServerDefaults.AuthenticationScheme);
            identity.AddClaim(OpenIdConnectConstants.Claims.Subject, "Bob le Bricoleur");

            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                new AuthenticationProperties(),
                OpenIdConnectServerDefaults.AuthenticationScheme);

            ticket.SetPresenters("Fabrikam");
            ticket.SetTokenId("3E228451-1555-46F7-A471-951EFBA23A56");

            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(ticket);

            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.IsValidAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                instance.Setup(mock => mock.GetCreationDateAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<DateTimeOffset?>(new DateTimeOffset(2017, 01, 01, 00, 00, 00, TimeSpan.Zero)));

                instance.Setup(mock => mock.GetExpirationDateAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<DateTimeOffset?>(new DateTimeOffset(2017, 01, 10, 00, 00, 00, TimeSpan.Zero)));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Token, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Services.AddSingleton(manager);

                builder.Configure(options =>
                {
                    options.SystemClock = Mock.Of<ISystemClock>(mock => mock.UtcNow ==
                        new DateTimeOffset(2017, 01, 05, 00, 00, 00, TimeSpan.Zero));

                    options.AuthorizationCodeFormat = format.Object;
                });
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                ClientSecret = "7Fjfp0ZBr1KtDRbnfVdmIw",
                Code = "2YotnFZFEjr1zCsicMWpAA",
                GrantType = OpenIdConnectConstants.GrantTypes.AuthorizationCode,
                RedirectUri = "http://www.fabrikam.com/path"
            });

            // Assert
            Assert.NotNull(response.AccessToken);

            Mock.Get(manager).Verify(mock => mock.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()), Times.Once());
            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Once());
        }

        [Fact]
        public async Task DeserializeRefreshToken_ReturnsNullForMalformedReferenceToken()
        {
            // Arrange
            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();
            var manager = CreateTokenManager();

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Introspection, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Confidential));

                    instance.Setup(mock => mock.ValidateClientSecretAsync(application, "7Fjfp0ZBr1KtDRbnfVdmIw", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                }));

                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();

                builder.Configure(options => options.RefreshTokenFormat = format.Object);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.RefreshToken,
                RefreshToken = "2YotnFZFEjr1zCsicMWpAA"
            });

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.InvalidGrant, response.Error);
            Assert.Equal("The specified refresh token is invalid.", response.ErrorDescription);

            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Never());
            Mock.Get(manager).Verify(mock => mock.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task DeserializeRefreshToken_RefreshTokenIsNotRetrievedUsingHashWhenReferenceTokensAreDisabled()
        {
            // Arrange
            var identity = new ClaimsIdentity(OpenIdConnectServerDefaults.AuthenticationScheme);
            identity.AddClaim(OpenIdConnectConstants.Claims.Subject, "Bob le Bricoleur");

            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                new AuthenticationProperties(),
                OpenIdConnectServerDefaults.AuthenticationScheme);

            ticket.SetTokenId("3E228451-1555-46F7-A471-951EFBA23A56");

            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(ticket);

            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.IsValidAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(manager);

                builder.Configure(options => options.RefreshTokenFormat = format.Object);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.RefreshToken,
                RefreshToken = "2YotnFZFEjr1zCsicMWpAA"
            });

            // Assert
            Assert.NotNull(response.AccessToken);

            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Once());
            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task DeserializeRefreshToken_ReturnsNullForMissingReferenceTokenIdentifier()
        {
            // Arrange
            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>(result: null));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.RefreshToken,
                RefreshToken = "HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ"
            });

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.InvalidGrant, response.Error);
            Assert.Equal("The specified refresh token is invalid.", response.ErrorDescription);

            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()), Times.Once());
            Mock.Get(manager).Verify(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        }

        [Fact]
        public async Task DeserializeRefreshToken_ReturnsNullForMissingReferenceTokenCiphertext()
        {
            // Arrange
            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.GetPayloadAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>(result: null));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.RefreshToken,
                RefreshToken = "HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ"
            });

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.InvalidGrant, response.Error);
            Assert.Equal("The specified refresh token is invalid.", response.ErrorDescription);

            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()), Times.Once());
            Mock.Get(manager).Verify(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        }

        [Fact]
        public async Task DeserializeRefreshToken_ReturnsNullForInvalidReferenceTokenCiphertext()
        {
            // Arrange
            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(value: null);

            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.GetPayloadAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("2YotnFZFEjr1zCsicMWpAA"));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();

                builder.Configure(options => options.RefreshTokenFormat = format.Object);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.RefreshToken,
                RefreshToken = "HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ"
            });

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.InvalidGrant, response.Error);
            Assert.Equal("The specified refresh token is invalid.", response.ErrorDescription);

            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()), Times.Once());
            Mock.Get(manager).Verify(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Once());
        }

        [Fact]
        public async Task DeserializeRefreshToken_ReturnsExpectedReferenceToken()
        {
            // Arrange
            var identity = new ClaimsIdentity(OpenIdConnectServerDefaults.AuthenticationScheme);
            identity.AddClaim(OpenIdConnectConstants.Claims.Subject, "Bob le Bricoleur");

            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                new AuthenticationProperties(),
                OpenIdConnectServerDefaults.AuthenticationScheme);

            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(ticket);

            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.GetPayloadAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("2YotnFZFEjr1zCsicMWpAA"));

                instance.Setup(mock => mock.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.IsValidAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                instance.Setup(mock => mock.GetCreationDateAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<DateTimeOffset?>(new DateTimeOffset(2017, 01, 01, 00, 00, 00, TimeSpan.Zero)));

                instance.Setup(mock => mock.GetExpirationDateAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<DateTimeOffset?>(new DateTimeOffset(2017, 01, 10, 00, 00, 00, TimeSpan.Zero)));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();

                builder.Configure(options =>
                {
                    options.SystemClock = Mock.Of<ISystemClock>(mock => mock.UtcNow ==
                        new DateTimeOffset(2017, 01, 05, 00, 00, 00, TimeSpan.Zero));

                    options.RefreshTokenFormat = format.Object;
                });
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.RefreshToken,
                RefreshToken = "HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ"
            });

            // Assert
            Assert.NotNull(response.AccessToken);

            Mock.Get(manager).Verify(mock => mock.FindByReferenceIdAsync("HQnldPTjH_9m85GcS-5PPYaCxmJTt1umxOa2y9ggVUQ", It.IsAny<CancellationToken>()), Times.Once());
            Mock.Get(manager).Verify(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()), Times.Once());
            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Once());
        }

        [Fact]
        public async Task DeserializeRefreshToken_ReturnsNullForMissingTokenIdentifier()
        {
            // Arrange
            var identity = new ClaimsIdentity(OpenIdConnectServerDefaults.AuthenticationScheme);
            identity.AddClaim(OpenIdConnectConstants.Claims.Subject, "Bob le Bricoleur");

            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                new AuthenticationProperties(),
                OpenIdConnectServerDefaults.AuthenticationScheme);

            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(ticket);

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Configure(options => options.RefreshTokenFormat = format.Object);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.RefreshToken,
                RefreshToken = "2YotnFZFEjr1zCsicMWpAA"
            });

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.InvalidGrant, response.Error);
            Assert.Equal("The specified refresh token is invalid.", response.ErrorDescription);
        }

        [Fact]
        public async Task DeserializeRefreshToken_ReturnsNullForInvalidTokenCiphertext()
        {
            // Arrange
            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(value: null);

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Configure(options => options.RefreshTokenFormat = format.Object);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.RefreshToken,
                RefreshToken = "2YotnFZFEjr1zCsicMWpAA"
            });

            // Assert
            Assert.Equal(OpenIdConnectConstants.Errors.InvalidGrant, response.Error);
            Assert.Equal("The specified refresh token is invalid.", response.ErrorDescription);

            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Once());
        }

        [Fact]
        public async Task DeserializeRefreshToken_ReturnsExpectedToken()
        {
            // Arrange
            var identity = new ClaimsIdentity(OpenIdConnectServerDefaults.AuthenticationScheme);
            identity.AddClaim(OpenIdConnectConstants.Claims.Subject, "Bob le Bricoleur");

            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                new AuthenticationProperties(),
                OpenIdConnectServerDefaults.AuthenticationScheme);

            ticket.SetTokenId("3E228451-1555-46F7-A471-951EFBA23A56");

            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"))
                .Returns(ticket);

            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.IsValidAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                instance.Setup(mock => mock.GetCreationDateAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<DateTimeOffset?>(new DateTimeOffset(2017, 01, 01, 00, 00, 00, TimeSpan.Zero)));

                instance.Setup(mock => mock.GetExpirationDateAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<DateTimeOffset?>(new DateTimeOffset(2017, 01, 10, 00, 00, 00, TimeSpan.Zero)));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(manager);

                builder.Configure(options =>
                {
                    options.SystemClock = Mock.Of<ISystemClock>(mock => mock.UtcNow ==
                        new DateTimeOffset(2017, 01, 05, 00, 00, 00, TimeSpan.Zero));

                    options.RefreshTokenFormat = format.Object;
                });
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.RefreshToken,
                RefreshToken = "2YotnFZFEjr1zCsicMWpAA"
            });

            // Assert
            Assert.NotNull(response.AccessToken);

            Mock.Get(manager).Verify(mock => mock.FindByIdAsync("3E228451-1555-46F7-A471-951EFBA23A56", It.IsAny<CancellationToken>()), Times.Once());
            format.Verify(mock => mock.Unprotect("2YotnFZFEjr1zCsicMWpAA"), Times.Once());
        }

        [Fact]
        public async Task SerializeAccessToken_AccessTokenIsNotPersistedWhenReferenceTokensAreDisabled()
        {
            // Arrange
            var manager = CreateTokenManager();

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(manager);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.Password,
                Username = "johndoe",
                Password = "A3ddj3w",
                Scope = OpenIdConnectConstants.Scopes.OfflineAccess
            });

            // Assert
            Assert.NotNull(response.AccessToken);

            Mock.Get(manager).Verify(mock => mock.CreateAsync(
                It.Is<OpenIddictTokenDescriptor>(descriptor =>
                    descriptor.Subject == "Bob le Magnifique" &&
                    descriptor.Type == OpenIdConnectConstants.TokenTypeHints.AccessToken),
                It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task SerializeAccessToken_ReferenceAccessTokenIsCorrectlyPersisted()
        {
            // Arrange
            var token = new OpenIddictToken
            {
                CreationDate = new DateTimeOffset(2017, 01, 02, 00, 00, 00, TimeSpan.Zero),
                ExpirationDate = new DateTimeOffset(2017, 01, 01, 00, 00, 00, TimeSpan.Zero)
            };

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.ObfuscateReferenceIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("B1F0D503-55A4-4B03-B05B-EF07713C18E1");
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();

                builder.Configure(options =>
                {
                    options.SystemClock = Mock.Of<ISystemClock>(mock => mock.UtcNow == token.CreationDate.Value);
                    options.AccessTokenLifetime = token.ExpirationDate.Value - token.CreationDate.Value;
                });
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.Password,
                Username = "johndoe",
                Password = "A3ddj3w",
                Scope = OpenIdConnectConstants.Scopes.OfflineAccess
            });

            // Assert
            Assert.NotNull(response.AccessToken);

            Mock.Get(manager).Verify(mock => mock.ObfuscateReferenceIdAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            Mock.Get(manager).Verify(mock => mock.CreateAsync(
                It.Is<OpenIddictTokenDescriptor>(descriptor =>
                    descriptor.ExpirationDate == token.ExpirationDate &&
                    descriptor.CreationDate == token.CreationDate &&
                    descriptor.Payload != null &&
                    descriptor.ReferenceId != null &&
                    descriptor.Subject == "Bob le Magnifique" &&
                    descriptor.Type == OpenIdConnectConstants.TokenTypeHints.AccessToken),
                It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task SerializeAccessToken_ClientApplicationIsAutomaticallyAttached()
        {
            // Arrange
            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Token, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.Password, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Public));

                    instance.Setup(mock => mock.GetIdAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));
                }));

                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                GrantType = OpenIdConnectConstants.GrantTypes.Password,
                Username = "johndoe",
                Password = "A3ddj3w",
                Scope = OpenIdConnectConstants.Scopes.OfflineAccess
            });

            // Assert
            Assert.NotNull(response.AccessToken);

            Mock.Get(manager).Verify(mock => mock.CreateAsync(
                It.Is<OpenIddictTokenDescriptor>(descriptor =>
                    descriptor.ApplicationId == "3E228451-1555-46F7-A471-951EFBA23A56" &&
                    descriptor.Subject == "Bob le Magnifique" &&
                    descriptor.Type == OpenIdConnectConstants.TokenTypeHints.AccessToken),
                It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task SerializeAccessToken_AuthorizationIsAutomaticallyAttached()
        {
            // Arrange
            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateAuthorizationManager(instance =>
                {
                    instance.Setup(mock => mock.FindByIdAsync("1AF06AB2-A0FC-4E3D-86AF-E04DA8C7BE70", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new OpenIddictAuthorization());
                }));

                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.Password,
                Username = "johndoe",
                Password = "A3ddj3w",
                Scope = OpenIdConnectConstants.Scopes.OfflineAccess,
                ["attach-authorization"] = true
            });

            // Assert
            Assert.NotNull(response.AccessToken);

            Mock.Get(manager).Verify(mock => mock.CreateAsync(
                It.Is<OpenIddictTokenDescriptor>(descriptor =>
                    descriptor.AuthorizationId == "1AF06AB2-A0FC-4E3D-86AF-E04DA8C7BE70" &&
                    descriptor.Subject == "Bob le Magnifique" &&
                    descriptor.Type == OpenIdConnectConstants.TokenTypeHints.AccessToken),
                It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task SerializeAuthorizationCode_AuthorizationCodeIsNotPersistedWhenRevocationIsDisabled()
        {
            // Arrange
            var manager = CreateTokenManager();

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Authorization, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.ValidateRedirectUriAsync(application, "http://www.fabrikam.com/path", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Public));
                }));

                builder.Services.AddSingleton(manager);

                builder.Configure(options => options.RevocationEndpointPath = PathString.Empty);

                builder.DisableTokenRevocation();
                builder.DisableSlidingExpiration();
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(AuthorizationEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                RedirectUri = "http://www.fabrikam.com/path",
                ResponseType = OpenIdConnectConstants.ResponseTypes.Code
            });

            // Assert
            Assert.NotNull(response.Code);

            Mock.Get(manager).Verify(mock => mock.CreateAsync(
                It.IsAny<OpenIddictTokenDescriptor>(),
                It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task SerializeAuthorizationCode_AuthorizationCodeIsCorrectlyPersisted()
        {
            // Arrange
            var token = new OpenIddictToken
            {
                CreationDate = new DateTimeOffset(2017, 01, 01, 00, 00, 00, TimeSpan.Zero),
                ExpirationDate = new DateTimeOffset(2017, 01, 02, 00, 00, 00, TimeSpan.Zero)
            };

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Authorization, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.ValidateRedirectUriAsync(application, "http://www.fabrikam.com/path", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Public));

                    instance.Setup(mock => mock.GetIdAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));
                }));

                builder.Services.AddSingleton(manager);

                builder.Configure(options =>
                {
                    options.SystemClock = Mock.Of<ISystemClock>(mock => mock.UtcNow == token.CreationDate.Value);
                    options.AuthorizationCodeLifetime = token.ExpirationDate.Value - token.CreationDate.Value;
                });
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(AuthorizationEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                RedirectUri = "http://www.fabrikam.com/path",
                ResponseType = OpenIdConnectConstants.ResponseTypes.Code
            });

            // Assert
            Assert.NotNull(response.Code);

            Mock.Get(manager).Verify(mock => mock.CreateAsync(
                It.Is<OpenIddictTokenDescriptor>(descriptor =>
                    descriptor.ExpirationDate == token.ExpirationDate &&
                    descriptor.CreationDate == token.CreationDate &&
                    descriptor.Payload == null &&
                    descriptor.ReferenceId == null &&
                    descriptor.Subject == "Bob le Magnifique" &&
                    descriptor.Type == OpenIdConnectConstants.TokenTypeHints.AuthorizationCode),
                It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task SerializeAuthorizationCode_ReferenceAuthorizationCodeIsCorrectlyPersisted()
        {
            // Arrange
            var token = new OpenIddictToken
            {
                CreationDate = new DateTimeOffset(2017, 01, 01, 00, 00, 00, TimeSpan.Zero),
                ExpirationDate = new DateTimeOffset(2017, 01, 02, 00, 00, 00, TimeSpan.Zero)
            };

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.ObfuscateReferenceIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("B1F0D503-55A4-4B03-B05B-EF07713C18E1");
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Authorization, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.ValidateRedirectUriAsync(application, "http://www.fabrikam.com/path", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Public));

                    instance.Setup(mock => mock.GetIdAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));
                }));

                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();

                builder.Configure(options =>
                {
                    options.SystemClock = Mock.Of<ISystemClock>(mock => mock.UtcNow == token.CreationDate.Value);
                    options.AuthorizationCodeLifetime = token.ExpirationDate.Value - token.CreationDate.Value;
                });
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(AuthorizationEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                RedirectUri = "http://www.fabrikam.com/path",
                ResponseType = OpenIdConnectConstants.ResponseTypes.Code
            });

            // Assert
            Assert.NotNull(response.Code);

            Mock.Get(manager).Verify(mock => mock.ObfuscateReferenceIdAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once());

            Mock.Get(manager).Verify(mock => mock.CreateAsync(
                It.Is<OpenIddictTokenDescriptor>(descriptor =>
                    descriptor.ExpirationDate == token.ExpirationDate &&
                    descriptor.CreationDate == token.CreationDate &&
                    descriptor.Payload != null &&
                    descriptor.ReferenceId != null &&
                    descriptor.Subject == "Bob le Magnifique" &&
                    descriptor.Type == OpenIdConnectConstants.TokenTypeHints.AuthorizationCode),
                It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task SerializeAuthorizationCode_ClientApplicationIsAutomaticallyAttached()
        {
            // Arrange
            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Authorization, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.ValidateRedirectUriAsync(application, "http://www.fabrikam.com/path", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Public));

                    instance.Setup(mock => mock.GetIdAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));
                }));

                builder.Services.AddSingleton(manager);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(AuthorizationEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                RedirectUri = "http://www.fabrikam.com/path",
                ResponseType = OpenIdConnectConstants.ResponseTypes.Code
            });

            // Assert
            Assert.NotNull(response.Code);

            Mock.Get(manager).Verify(mock => mock.CreateAsync(
                It.Is<OpenIddictTokenDescriptor>(descriptor =>
                    descriptor.ApplicationId == "3E228451-1555-46F7-A471-951EFBA23A56" &&
                    descriptor.Subject == "Bob le Magnifique" &&
                    descriptor.Type == OpenIdConnectConstants.TokenTypeHints.AuthorizationCode),
                It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task SerializeAuthorizationCode_AuthorizationIsAutomaticallyAttached()
        {
            // Arrange
            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Authorization, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.ValidateRedirectUriAsync(application, "http://www.fabrikam.com/path", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Public));

                    instance.Setup(mock => mock.GetIdAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));
                }));

                builder.Services.AddSingleton(CreateAuthorizationManager(instance =>
                {
                    instance.Setup(mock => mock.FindByIdAsync("1AF06AB2-A0FC-4E3D-86AF-E04DA8C7BE70", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new OpenIddictAuthorization());
                }));

                builder.Services.AddSingleton(manager);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(AuthorizationEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                RedirectUri = "http://www.fabrikam.com/path",
                ResponseType = OpenIdConnectConstants.ResponseTypes.Code,
                ["attach-authorization"] = true
            });

            // Assert
            Assert.NotNull(response.Code);

            Mock.Get(manager).Verify(mock => mock.CreateAsync(
                It.Is<OpenIddictTokenDescriptor>(descriptor =>
                    descriptor.ApplicationId == "3E228451-1555-46F7-A471-951EFBA23A56" &&
                    descriptor.AuthorizationId == "1AF06AB2-A0FC-4E3D-86AF-E04DA8C7BE70" &&
                    descriptor.Subject == "Bob le Magnifique" &&
                    descriptor.Type == OpenIdConnectConstants.TokenTypeHints.AuthorizationCode),
                It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task SerializeRefreshToken_ExpirationDateIsFixedWhenSlidingExpirationIsDisabled()
        {
            // Arrange
            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(),
                new AuthenticationProperties(),
                OpenIdConnectServerDefaults.AuthenticationScheme);

            ticket.SetTokenId("60FFF7EA-F98E-437B-937E-5073CC313103");
            ticket.SetScopes(OpenIdConnectConstants.Scopes.OpenId, OpenIdConnectConstants.Scopes.OfflineAccess);

            var format = new Mock<ISecureDataFormat<AuthenticationTicket>>();

            format.Setup(mock => mock.Protect(It.IsAny<AuthenticationTicket>()))
                .Returns("8xLOxBtZp8");

            format.Setup(mock => mock.Unprotect("8xLOxBtZp8"))
                .Returns(ticket);

            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.FindByIdAsync("60FFF7EA-F98E-437B-937E-5073CC313103", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("60FFF7EA-F98E-437B-937E-5073CC313103951EFBA23A56"));

                instance.Setup(mock => mock.IsRedeemedAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                instance.Setup(mock => mock.IsValidAsync(token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(manager);

                builder.DisableSlidingExpiration();
                builder.UseRollingTokens();

                builder.Configure(options =>
                {
                    options.SystemClock = Mock.Of<ISystemClock>(mock => mock.UtcNow ==
                        new DateTimeOffset(2017, 01, 05, 00, 00, 00, TimeSpan.Zero));
                    options.RefreshTokenLifetime = TimeSpan.FromDays(10);
                    options.RefreshTokenFormat = format.Object;
                });
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.RefreshToken,
                RefreshToken = "8xLOxBtZp8"
            });

            // Assert
            Assert.NotNull(response.RefreshToken);

            Mock.Get(manager).Verify(mock => mock.ExtendAsync(token,
                new DateTimeOffset(2017, 01, 10, 00, 00, 00, TimeSpan.Zero),
                It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task SerializeRefreshToken_RefreshTokenIsNotPersistedWhenRevocationIsDisabled()
        {
            // Arrange
            var manager = CreateTokenManager();

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(manager);

                builder.Configure(options => options.RevocationEndpointPath = PathString.Empty);

                builder.DisableTokenRevocation();
                builder.DisableSlidingExpiration();
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.Password,
                Username = "johndoe",
                Password = "A3ddj3w",
                Scope = OpenIdConnectConstants.Scopes.OfflineAccess
            });

            // Assert
            Assert.NotNull(response.RefreshToken);

            Mock.Get(manager).Verify(mock => mock.CreateAsync(
                It.IsAny<OpenIddictTokenDescriptor>(),
                It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task SerializeRefreshToken_RefreshTokenIsCorrectlyPersisted()
        {
            // Arrange
            var token = new OpenIddictToken
            {
                CreationDate = new DateTimeOffset(2017, 01, 01, 00, 00, 00, TimeSpan.Zero),
                ExpirationDate = new DateTimeOffset(2017, 01, 02, 00, 00, 00, TimeSpan.Zero)
            };

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(manager);

                builder.Configure(options =>
                {
                    options.SystemClock = Mock.Of<ISystemClock>(mock => mock.UtcNow == token.CreationDate.Value);
                    options.RefreshTokenLifetime = token.ExpirationDate.Value - token.CreationDate.Value;
                });
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.Password,
                Username = "johndoe",
                Password = "A3ddj3w",
                Scope = OpenIdConnectConstants.Scopes.OfflineAccess
            });

            // Assert
            Assert.NotNull(response.RefreshToken);

            Mock.Get(manager).Verify(mock => mock.CreateAsync(
                It.Is<OpenIddictTokenDescriptor>(descriptor =>
                    descriptor.ExpirationDate == token.ExpirationDate &&
                    descriptor.CreationDate == token.CreationDate &&
                    descriptor.Payload == null &&
                    descriptor.ReferenceId == null &&
                    descriptor.Subject == "Bob le Magnifique" &&
                    descriptor.Type == OpenIdConnectConstants.TokenTypeHints.RefreshToken),
                It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task SerializeRefreshToken_ReferenceRefreshTokenIsCorrectlyPersisted()
        {
            // Arrange
            var token = new OpenIddictToken
            {
                CreationDate = new DateTimeOffset(2017, 01, 01, 00, 00, 00, TimeSpan.Zero),
                ExpirationDate = new DateTimeOffset(2017, 01, 02, 00, 00, 00, TimeSpan.Zero)
            };

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));

                instance.Setup(mock => mock.ObfuscateReferenceIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("B1F0D503-55A4-4B03-B05B-EF07713C18E1");
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(manager);

                builder.UseReferenceTokens();

                builder.Configure(options =>
                {
                    options.SystemClock = Mock.Of<ISystemClock>(mock => mock.UtcNow == token.CreationDate.Value);
                    options.RefreshTokenLifetime = token.ExpirationDate.Value - token.CreationDate.Value;
                });
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.Password,
                Username = "johndoe",
                Password = "A3ddj3w",
                Scope = OpenIdConnectConstants.Scopes.OfflineAccess
            });

            // Assert
            Assert.NotNull(response.RefreshToken);

            Mock.Get(manager).Verify(mock => mock.ObfuscateReferenceIdAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            Mock.Get(manager).Verify(mock => mock.CreateAsync(
                It.Is<OpenIddictTokenDescriptor>(descriptor =>
                    descriptor.ExpirationDate == token.ExpirationDate &&
                    descriptor.CreationDate == token.CreationDate &&
                    descriptor.Payload != null &&
                    descriptor.ReferenceId == "B1F0D503-55A4-4B03-B05B-EF07713C18E1" &&
                    descriptor.Subject == "Bob le Magnifique" &&
                    descriptor.Type == OpenIdConnectConstants.TokenTypeHints.RefreshToken),
                It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task SerializeRefreshToken_ClientApplicationIsAutomaticallyAttached()
        {
            // Arrange
            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateApplicationManager(instance =>
                {
                    var application = new OpenIddictApplication();

                    instance.Setup(mock => mock.FindByClientIdAsync("Fabrikam", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(application);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.Endpoints.Token, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.HasPermissionAsync(application,
                        OpenIddictConstants.Permissions.GrantTypes.Password, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

                    instance.Setup(mock => mock.GetClientTypeAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>(OpenIddictConstants.ClientTypes.Public));

                    instance.Setup(mock => mock.GetIdAsync(application, It.IsAny<CancellationToken>()))
                        .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));
                }));

                builder.Services.AddSingleton(manager);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                ClientId = "Fabrikam",
                GrantType = OpenIdConnectConstants.GrantTypes.Password,
                Username = "johndoe",
                Password = "A3ddj3w",
                Scope = OpenIdConnectConstants.Scopes.OfflineAccess
            });

            // Assert
            Assert.NotNull(response.RefreshToken);

            Mock.Get(manager).Verify(mock => mock.CreateAsync(
                It.Is<OpenIddictTokenDescriptor>(descriptor =>
                    descriptor.ApplicationId == "3E228451-1555-46F7-A471-951EFBA23A56" &&
                    descriptor.Subject == "Bob le Magnifique" &&
                    descriptor.Type == OpenIdConnectConstants.TokenTypeHints.RefreshToken),
                It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task SerializeRefreshToken_AuthorizationIsAutomaticallyAttached()
        {
            // Arrange
            var token = new OpenIddictToken();

            var manager = CreateTokenManager(instance =>
            {
                instance.Setup(mock => mock.CreateAsync(It.IsAny<OpenIddictTokenDescriptor>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(token);

                instance.Setup(mock => mock.GetIdAsync(token, It.IsAny<CancellationToken>()))
                    .Returns(new ValueTask<string>("3E228451-1555-46F7-A471-951EFBA23A56"));
            });

            var server = CreateAuthorizationServer(builder =>
            {
                builder.Services.AddSingleton(CreateAuthorizationManager(instance =>
                {
                    instance.Setup(mock => mock.FindByIdAsync("1AF06AB2-A0FC-4E3D-86AF-E04DA8C7BE70", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new OpenIddictAuthorization());
                }));

                builder.Services.AddSingleton(manager);
            });

            var client = new OpenIdConnectClient(server.CreateClient());

            // Act
            var response = await client.PostAsync(TokenEndpoint, new OpenIdConnectRequest
            {
                GrantType = OpenIdConnectConstants.GrantTypes.Password,
                Username = "johndoe",
                Password = "A3ddj3w",
                Scope = OpenIdConnectConstants.Scopes.OfflineAccess,
                ["attach-authorization"] = true
            });

            // Assert
            Assert.NotNull(response.RefreshToken);

            Mock.Get(manager).Verify(mock => mock.CreateAsync(
                It.Is<OpenIddictTokenDescriptor>(descriptor =>
                    descriptor.AuthorizationId == "1AF06AB2-A0FC-4E3D-86AF-E04DA8C7BE70" &&
                    descriptor.Subject == "Bob le Magnifique" &&
                    descriptor.Type == OpenIdConnectConstants.TokenTypeHints.RefreshToken),
                It.IsAny<CancellationToken>()), Times.Once());
        }
    }
}

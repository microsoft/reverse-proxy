// <copyright file="ServiceFabricExtensionConfigProviderTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.ServiceFabric.Tests
{
    public class ServiceExtensionLabelsProviderTests : TestAutoMockBase
    {
        private const string ApplicationName = "fabric:/App1";
        private const string ApplicationTypeName = "AppType1";
        private const string ApplicationTypeVersion = "6.7.8";
        private const string ServiceManifestName = "ThisCoolTestManifest";
        private const string ServiceTypeName = "WebServiceType";
        private const string ServiceFabricName = "fabric:/App1/SvcName";
        private readonly Dictionary<string, string> _appParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private string _rawServiceManifest = "<xml></xml>";
        private Dictionary<string, string> _namingServiceProperties = new Dictionary<string, string>();

        public ServiceExtensionLabelsProviderTests()
        {
            Mock<IServiceFabricCaller>()
                .Setup(
                    m => m.GetServiceManifestName(
                        ApplicationTypeName,
                        ApplicationTypeVersion,
                        ServiceTypeName,
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => ServiceManifestName);

            Mock<IServiceFabricCaller>()
                .Setup(
                    m => m.GetServiceManifestAsync(
                        ApplicationTypeName,
                        ApplicationTypeVersion,
                        ServiceManifestName,
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _rawServiceManifest);

            Mock<IServiceFabricCaller>()
                .Setup(
                    m => m.EnumeratePropertiesAsync(
                        It.Is<Uri>(name => name.ToString() == ServiceFabricName),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _namingServiceProperties);
        }

        [Fact]
        public async void GetExtensionLabels_UnexistingServiceTypeName_NoLabels()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                  <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='AnotherService'>
                    </StatelessServiceType>
                  </ServiceTypes>
                </ServiceManifest>";

            // Act
            var labels = await RunScenarioAsync();

            // TODO: consider throwing if the servicetypename does not exist
            // Assert
            labels.Should().Equal(new Dictionary<string, string>());
        }

        [Fact]
        public async void GetExtensionLabels_NoExtensions_NoLabels()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                  <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                    </StatelessServiceType>
                  </ServiceTypes>
                </ServiceManifest>";

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(new Dictionary<string, string>());
        }

        [Fact]
        public async void GetExtensionLabels_NoLabelsInExtensions_NoLabels()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                        </Extensions>
                    </StatelessServiceType>
                    <StatelessServiceType ServiceTypeName='AnotherServiceType'>
                        <Extensions>
                            <Extension Name='IslandGateway'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(new Dictionary<string, string>());
        }

        [Fact]
        public async void GetExtensionLabels_NoIslandGatewayExtensions_NoLabels()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='AnotherExtension'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                                <Label Key='Bla'>foo</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(new Dictionary<string, string>());
        }

        [Fact]
        public async void GetExtensionLabels_LabelsInManifestExtensions_GatheredCorrectly()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='IslandGateway'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.foo'>bar</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.foo", "bar" },
                });
        }

        [Fact]
        public async void GetExtensionLabels_AppParamReplacements_Replaces()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                  <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                      <Extensions>
                        <Extension Name='IslandGateway'>
                          <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                            <Label Key='IslandGateway.Enable'>true</Label>
                            <Label Key='IslandGateway.foo'>[SomeAppParam]</Label>
                          </Labels>
                        </Extension>
                      </Extensions>
                    </StatelessServiceType>
                  </ServiceTypes>
                </ServiceManifest>";

            _appParams["someAppParam"] = "replaced successfully!";

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.foo", "replaced successfully!" },
                });
        }

        [Fact]
        public async void GetExtensionLabels_AppParamReplacements_MissingAppParams_ReplacesWithEmpty()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                  <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                      <Extensions>
                        <Extension Name='IslandGateway'>
                          <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                            <Label Key='IslandGateway.Enable'>true</Label>
                            <Label Key='IslandGateway.foo'>[NonExistingAppParam]</Label>
                          </Labels>
                        </Extension>
                      </Extensions>
                    </StatelessServiceType>
                  </ServiceTypes>
                </ServiceManifest>";

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.foo", string.Empty },
                });
        }

        [Fact]
        public async void GetExtensionLabels_MultipleExtensions_OnlyIslandGatewayLabelsAreGathered()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='AnotherExtension'>
                            <Labels>
                                <Label Key='NotThisONe'>I said not this one</Label>
                            </Labels>
                            </Extension>
                            <Extension Name='IslandGateway'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.routes.route1.Hosts'>example.com</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.Hosts", "example.com" },
                });
        }

        [Fact]
        public async void GetExtensionLabels_OverridesEnabled_NewLabelsFromPropertiesGatheredCorrectly()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='IslandGateway'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.EnableDynamicOverrides'>true</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";
            _namingServiceProperties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.Hosts", "example.com" },
                };

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.Hosts", "example.com" },
                    { "IslandGateway.EnableDynamicOverrides", "true" },
                });
        }

        [Fact]
        public async void GetExtensionLabels_OverridesEnabledValueCaseInsensitive_OverridesAreEnabled()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='IslandGateway'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.EnableDynamicOverrides'>True</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";
            _namingServiceProperties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.Hosts", "example.com" },
                };

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.Hosts", "example.com" },
                    { "IslandGateway.EnableDynamicOverrides", "True" },
                });
        }

        [Fact]
        public async void GetExtensionLabels_OverridesEnabled_OnlyIslandGatewayNamespacePropertiesGathered()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='IslandGateway'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.EnableDynamicOverrides'>true</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";
            _namingServiceProperties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "ISLANDGATEWAy.enable", "false" },
                    { "WhatIsThisNamespace.value", "42" },
                };

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.EnableDynamicOverrides", "true" },
                });
        }

        [Fact]
        public async void GetExtensionLabels_OverridesEnabled_PropertiesOverrideManifestCorrectly()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='IslandGateway'>
                             <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.EnableDynamicOverrides'>true</Label>
                                <Label Key='IslandGateway.routes.route1.Hosts'>example.com</Label>
                             </Labels>
                             </Extension>
                        </Extensions>
                    </StatefulService>
                </ServiceTypes>
                </ServiceManifest>
                ";
            _namingServiceProperties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "false" },
                };

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "false" },
                    { "IslandGateway.routes.route1.Hosts", "example.com" },
                    { "IslandGateway.EnableDynamicOverrides", "true" },
                });
        }

        [Fact]
        public async void GetExtensionLabels_OverridesEnabled_LabelKeysAreCaseSensitive()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='IslandGateway'>
                             <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.EnableDynamicOverrides'>true</Label>
                                <Label Key='IslandGateway.routes.route1.Hosts'>example.com</Label>
                             </Labels>
                             </Extension>
                        </Extensions>
                    </StatefulService>
                </ServiceTypes>
                </ServiceManifest>
                ";
            _namingServiceProperties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.Routes.route1.HOST", "another.example.com" },
                };

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.Hosts", "example.com" },
                    { "IslandGateway.EnableDynamicOverrides", "true" },
                    { "IslandGateway.Routes.route1.HOST", "another.example.com" },
                });
        }

        [Fact]
        public async void GetExtensionLabels_OverridesDisabiled_PropertiesNotQueried()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='IslandGateway'>
                             <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.EnableDynamicOverrides'>false</Label>
                                <Label Key='IslandGateway.routes.route1.host'>example.com</Label>
                             </Labels>
                             </Extension>
                        </Extensions>
                    </StatefulService>
                </ServiceTypes>
                </ServiceManifest>
                ";
            _namingServiceProperties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "false" },
                };

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.host", "example.com" },
                    { "IslandGateway.EnableDynamicOverrides", "false" },
                });

            Mock<IServiceFabricCaller>().Verify(m => m.EnumeratePropertiesAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async void GetExtensionLabels_CaseDifferingLabelKeys_LabelKeysAreCaseSensitive()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='IslandGateway'>
                             <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.EnableDynamicOverrides'>true</Label>
                                <Label Key='IslandGateway.routes.ROUTE1.Hosts'>example.com</Label>
                                <Label Key='IslandGateway.routes.route1.hosts'>another.example.com</Label>
                             </Labels>
                             </Extension>
                        </Extensions>
                    </StatefulService>
                </ServiceTypes>
                </ServiceManifest>
                ";
            _namingServiceProperties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.routes.Route1.HOSTS", "etc.example.com" },
                };

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.ROUTE1.Hosts", "example.com" },
                    { "IslandGateway.routes.route1.hosts", "another.example.com" },
                    { "IslandGateway.routes.Route1.HOSTS", "etc.example.com" },
                    { "IslandGateway.EnableDynamicOverrides", "true" },
                });
        }

        [Fact]
        public async void GetExtensionLabels_OverridesNotExplicitlyDisabiled_PropertiesNotQueriedByDefault()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='IslandGateway'>
                             <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.routes.route1.host'>example.com</Label>
                             </Labels>
                             </Extension>
                        </Extensions>
                    </StatefulService>
                </ServiceTypes>
                </ServiceManifest>
                ";
            _namingServiceProperties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "false" },
                };

            // Act
            var labels = await RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.host", "example.com" },
                });

            Mock<IServiceFabricCaller>().Verify(m => m.EnumeratePropertiesAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async void GetExtensionLabels_InvalidManifestXml_Throws()
        {
            // Arrange
            _rawServiceManifest = $@"<this is no xml my man";

            // Act
            Func<Task> func = () => RunScenarioAsync();

            // Assert
            await func.Should().ThrowAsync<ConfigException>();
        }

        [Fact]
        public async void GetExtensionLabels_DuplicatedLabelKeysInManifest_ShouldThrowException()
        {
            // Arrange
            _rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='IslandGateway'>
                             <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.routes.route1.host'>example.com</Label>
                                <Label Key='IslandGateway.routes.route1.host'>example.com</Label>
                             </Labels>
                             </Extension>
                        </Extensions>
                    </StatefulService>
                </ServiceTypes>
                </ServiceManifest>
                ";

            // Act
            Func<Task> func = () => RunScenarioAsync();

            // Assert
            await func.Should().ThrowAsync<ConfigException>();
        }

        [Fact]
        public async void GetExtensionLabels_OverSizeLimit_ShouldThrowException()
        {
            // Arrange
            var longBadString = new string('*', 1024 * 1024);
            _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
            <ServiceTypes>
                <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                    <Extensions>
                        <Extension Name='IslandGateway'>
                        <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                             <Label Key='IslandGateway.foo'>'{longBadString}'</Label>
                          </Labels>
                        </Extension>
                    </Extensions>
                </StatelessServiceType>
            </ServiceTypes>
            </ServiceManifest>
            ";

            // Act
            Func<Task> func = () => RunScenarioAsync();

            // Assert
            await func.Should().ThrowAsync<ConfigException>();
        }

        [Fact]
        public async void GetExtensionLabels_WithDTD_ShouldThrowException()
        {
            // Arrange
            _rawServiceManifest =
                $@"
                <!DOCTYPE xxe [
                    <!ENTITY  chybeta  ""Melicious DTD value here."">
                ]>
                < ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='IslandGateway'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.foo'>bar</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

            // Act
            Func<Task> func = () => RunScenarioAsync();

            // Assert
            await func.Should().ThrowAsync<ConfigException>();
        }

        private async Task<Dictionary<string, string>> RunScenarioAsync()
        {
            var configProvider = Create<ServiceExtensionLabelsProvider>();

            return await configProvider.GetExtensionLabelsAsync(
                application: new ApplicationWrapper
                {
                    ApplicationName = new Uri(ApplicationName),
                    ApplicationTypeName = ApplicationTypeName,
                    ApplicationTypeVersion = ApplicationTypeVersion,
                    ApplicationParameters = _appParams,
                },
                service: new ServiceWrapper
                {
                    ServiceName = new Uri(ServiceFabricName),
                    ServiceTypeName = ServiceTypeName,
                },
                cancellationToken: CancellationToken.None);
        }
    }
}

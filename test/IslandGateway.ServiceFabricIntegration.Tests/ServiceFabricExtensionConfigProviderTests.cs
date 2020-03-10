// <copyright file="ServiceFabricExtensionConfigProviderTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;
using IslandGateway.Common.Abstractions.Telemetry;
using IslandGateway.Common.Telemetry;
using Moq;
using Tests.Common;
using Xunit;

namespace IslandGateway.ServiceFabricIntegration.Tests
{
    public class ServiceFabricExtensionConfigProviderTests : TestAutoMockBase
    {
        private const string ApplicationTypeName = "AppType1";
        private const string ApplicationTypeVersion = "6.7.8";
        private const string ServiceManifestName = "ThisCoolTestManifest";
        private const string ServiceTypeName = "WebServiceType";
        private const string ServiceFabricName = "fabric:/AppName/SvcName";
        private string rawServiceManifest = "<xml></xml>";
        private Dictionary<string, string> properties = new Dictionary<string, string>();

        public ServiceFabricExtensionConfigProviderTests()
        {
            this.Provide<IOperationLogger, TextOperationLogger>();

            this.Mock<IServiceFabricCaller>()
                .Setup(
                    m => m.GetServiceManifestName(
                        ApplicationTypeName,
                        ApplicationTypeVersion,
                        ServiceTypeName,
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => ServiceManifestName);

            this.Mock<IServiceFabricCaller>()
                .Setup(
                    m => m.GetServiceManifestAsync(
                        ApplicationTypeName,
                        ApplicationTypeVersion,
                        ServiceManifestName,
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => this.rawServiceManifest);

            this.Mock<IServiceFabricCaller>()
                .Setup(
                    m => m.EnumeratePropertiesAsync(
                        It.Is<Uri>(name => name.ToString() == ServiceFabricName),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => this.properties);
        }

        [Fact]
        public async void UnexistingServiceTypeName_GetExtensionLabels_NoLabels()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='AnotherService'>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

            // Act
            var labels = await this.RunScenarioAsync();

            // TODO: consider throwing if the servicetypename does not exist
            // Assert
            labels.Should().Equal(new Dictionary<string, string>());
        }

        [Fact]
        public async void NoExtensions_GetExtensionLabels_NoLabels()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

            // Act
            var labels = await this.RunScenarioAsync();

            // Assert
            labels.Should().Equal(new Dictionary<string, string>());
        }

        [Fact]
        public async void NoLabelsInExtensions_GetExtensionLabels_NoLabels()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                        </Extensions>
                    </StatelessServiceType>
                    <StatelessServiceType ServiceTypeName='AnotherServiceType'>
                        <Extensions>
                            <Extension Name='IslandGateway'>
                            <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

            // Act
            var labels = await this.RunScenarioAsync();

            // Assert
            labels.Should().Equal(new Dictionary<string, string>());
        }

        [Fact]
        public async void NoIslandGatewayExtensions_GetExtensionLabels_NoLabels()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='AnotherExtension'>
                            <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                                <Label Key='Bla'>foo</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

            // Act
            var labels = await this.RunScenarioAsync();

            // Assert
            labels.Should().Equal(new Dictionary<string, string>());
        }

        [Fact]
        public async void LabelsInManifestExtensions_GetExtensionLabels_GatheredCorrectly()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='IslandGateway'>
                            <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
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
            var labels = await this.RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.foo", "bar" },
                });
        }

        [Fact]
        public async void MultipleExtensions_GetExtensionLabels_OnlyIslandGatewayLabelsAreGathered()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='AnotherExtension'>
                            <Labels>
                                <Label Key='NotThisONe'>I said not this one</Label>
                            </Labels>
                            </Extension>
                            <Extension Name='IslandGateway'>
                            <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.routes.route1.rule'>Host('example.com')</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

            // Act
            var labels = await this.RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.rule", "Host('example.com')" },
                });
        }

        [Fact]
        public async void OverridesEnabled_GetExtensionLabels_NewLabelsFromPropertiesGatheredCorrectly()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='IslandGateway'>
                            <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.EnableDynamicOverrides'>true</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";
            this.properties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.rule", "Host('example.com')" },
                };

            // Act
            var labels = await this.RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.rule", "Host('example.com')" },
                    { "IslandGateway.EnableDynamicOverrides", "true" },
                });
        }

        [Fact]
        public async void OverridesEnabledValueCaseInsensitive_GetExtensionLabels_OverridesAreEnabled()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='IslandGateway'>
                            <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.EnableDynamicOverrides'>True</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";
            this.properties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.rule", "Host('example.com')" },
                };

            // Act
            var labels = await this.RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.rule", "Host('example.com')" },
                    { "IslandGateway.EnableDynamicOverrides", "True" },
                });
        }

        [Fact]
        public async void OverridesEnabled_GetExtensionLabels_OnlyIslandGatewayNamespacePropertiesGathered()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='IslandGateway'>
                            <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.EnableDynamicOverrides'>true</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";
            this.properties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "ISLANDGATEWAy.enable", "false" },
                    { "WhatIsThisNamespace.value", "42" },
                };

            // Act
            var labels = await this.RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.EnableDynamicOverrides", "true" },
                });
        }

        [Fact]
        public async void OverridesEnabled_GetExtensionLabels_PropertiesOverrideManifestCorrectly()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='IslandGateway'>
                             <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.EnableDynamicOverrides'>true</Label>
                                <Label Key='IslandGateway.routes.route1.rule'>Host('example.com')</Label>
                             </Labels>
                             </Extension>
                        </Extensions>
                    </StatefulService>
                </ServiceTypes>
                </ServiceManifest>
                ";
            this.properties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "false" },
                };

            // Act
            var labels = await this.RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "false" },
                    { "IslandGateway.routes.route1.rule", "Host('example.com')" },
                    { "IslandGateway.EnableDynamicOverrides", "true" },
                });
        }

        [Fact]
        public async void OverridesEnabled_GetExtensionLabels_LabelKeysAreCaseSensitive()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='IslandGateway'>
                             <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.EnableDynamicOverrides'>true</Label>
                                <Label Key='IslandGateway.routes.route1.rule'>Host('example.com')</Label>
                             </Labels>
                             </Extension>
                        </Extensions>
                    </StatefulService>
                </ServiceTypes>
                </ServiceManifest>
                ";
            this.properties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.Routes.route1.RULE", "Host('another.example.com')" },
                };

            // Act
            var labels = await this.RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.rule", "Host('example.com')" },
                    { "IslandGateway.EnableDynamicOverrides", "true" },
                    { "IslandGateway.Routes.route1.RULE", "Host('another.example.com')" },
                });
        }

        [Fact]
        public async void OverridesDisabiled_GetExtensionLabels_PropertiesNotQueried()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='IslandGateway'>
                             <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.EnableDynamicOverrides'>false</Label>
                                <Label Key='IslandGateway.routes.route1.rule'>Host('example.com')</Label>
                             </Labels>
                             </Extension>
                        </Extensions>
                    </StatefulService>
                </ServiceTypes>
                </ServiceManifest>
                ";
            this.properties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "false" },
                };

            // Act
            var labels = await this.RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.rule", "Host('example.com')" },
                    { "IslandGateway.EnableDynamicOverrides", "false" },
                });

            this.Mock<IServiceFabricCaller>().Verify(m => m.EnumeratePropertiesAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async void CaseDifferingLabelKeys_GetExtensionLabels_LabelKeysAreCaseSensitive()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='IslandGateway'>
                             <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.EnableDynamicOverrides'>true</Label>
                                <Label Key='IslandGateway.routes.ROUTE1.rule'>Host('example.com')</Label>
                                <Label Key='IslandGateway.routes.route1.rule'>Host('another.example.com')</Label>
                             </Labels>
                             </Extension>
                        </Extensions>
                    </StatefulService>
                </ServiceTypes>
                </ServiceManifest>
                ";
            this.properties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.routes.Route1.RULE", "Host('bla.foo')" },
                };

            // Act
            var labels = await this.RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.ROUTE1.rule", "Host('example.com')" },
                    { "IslandGateway.routes.route1.rule", "Host('another.example.com')" },
                    { "IslandGateway.routes.Route1.RULE", "Host('bla.foo')" },
                    { "IslandGateway.EnableDynamicOverrides", "true" },
                });
        }

        [Fact]
        public async void OverridesNotExplicitlyDisabiled_GetExtensionLabels_PropertiesNotQueriedByDefault()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='IslandGateway'>
                             <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.routes.route1.rule'>Host('example.com')</Label>
                             </Labels>
                             </Extension>
                        </Extensions>
                    </StatefulService>
                </ServiceTypes>
                </ServiceManifest>
                ";
            this.properties =
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "false" },
                };

            // Act
            var labels = await this.RunScenarioAsync();

            // Assert
            labels.Should().Equal(
                new Dictionary<string, string>
                {
                    { "IslandGateway.Enable", "true" },
                    { "IslandGateway.routes.route1.rule", "Host('example.com')" },
                });

            this.Mock<IServiceFabricCaller>().Verify(m => m.EnumeratePropertiesAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async void InvalidManifestXml_GetExtensionLabels_Throws()
        {
            // Arrange
            this.rawServiceManifest = $@"<this is no xml my man";

            // Act
            Func<Task> func = () => this.RunScenarioAsync();

            // Assert
            await func.Should().ThrowAsync<ServiceFabricIntegrationException>();
        }

        [Fact]
        public async void DuplicatedLabelKeysInManifest_GetExtensionLabels_Throws()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='IslandGateway'>
                             <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.Enable'>true</Label>
                                <Label Key='IslandGateway.routes.route1.rule'>Host('example.com')</Label>
                                <Label Key='IslandGateway.routes.route1.rule'>Host('example.com')</Label>
                             </Labels>
                             </Extension>
                        </Extensions>
                    </StatefulService>
                </ServiceTypes>
                </ServiceManifest>
                ";

            // Act
            Func<Task> func = () => this.RunScenarioAsync();

            // Assert
            await func.Should().ThrowAsync<ServiceFabricIntegrationException>();
        }

        private async Task<Dictionary<string, string>> RunScenarioAsync()
        {
            var configProvider = this.Create<ServiceFabricExtensionConfigProvider>();

            return await configProvider.GetExtensionLabelsAsync(
                applicationTypeName: ApplicationTypeName,
                applicationTypeVersion: ApplicationTypeVersion,
                serviceTypeName: ServiceTypeName,
                serviceFabricName: ServiceFabricName,
                cancellationToken: CancellationToken.None);
        }
    }
}
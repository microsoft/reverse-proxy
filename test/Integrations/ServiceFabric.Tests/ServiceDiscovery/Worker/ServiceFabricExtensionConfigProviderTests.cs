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

namespace Microsoft.ReverseProxy.ServiceFabricIntegration.Tests
{
    public class ServiceFabricExtensionConfigProviderTests : TestAutoMockBase
    {
        private const string ApplicationName = "fabric:/App1";
        private const string ApplicationTypeName = "AppType1";
        private const string ApplicationTypeVersion = "6.7.8";
        private const string ServiceManifestName = "ThisCoolTestManifest";
        private const string ServiceTypeName = "WebServiceType";
        private const string ServiceFabricName = "fabric:/App1/SvcName";
        private string rawServiceManifest = "<xml></xml>";
        private Dictionary<string, string> namingServiceProperties = new Dictionary<string, string>();
        private Dictionary<string, string> appParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public ServiceFabricExtensionConfigProviderTests()
        {
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
                .ReturnsAsync(() => this.namingServiceProperties);
        }

        [Fact]
        public async void GetExtensionLabels_UnexistingServiceTypeName_NoLabels()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                  <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='AnotherService'>
                    </StatelessServiceType>
                  </ServiceTypes>
                </ServiceManifest>";

            // Act
            var labels = await this.RunScenarioAsync();

            // TODO: consider throwing if the servicetypename does not exist
            // Assert
            labels.Should().Equal(new Dictionary<string, string>());
        }

        [Fact]
        public async void GetExtensionLabels_NoExtensions_NoLabels()
        {
            // Arrange
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                  <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                    </StatelessServiceType>
                  </ServiceTypes>
                </ServiceManifest>";

            // Act
            var labels = await this.RunScenarioAsync();

            // Assert
            labels.Should().Equal(new Dictionary<string, string>());
        }

        [Fact]
        public async void GetExtensionLabels_NoLabelsInExtensions_NoLabels()
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
        public async void GetExtensionLabels_NoIslandGatewayExtensions_NoLabels()
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
        public async void GetExtensionLabels_LabelsInManifestExtensions_GatheredCorrectly()
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
        public async void GetExtensionLabels_AppParamReplacements_Replaces()
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
                            <Label Key='IslandGateway.foo'>[SomeAppParam]</Label>
                          </Labels>
                        </Extension>
                      </Extensions>
                    </StatelessServiceType>
                  </ServiceTypes>
                </ServiceManifest>";

            this.appParams["someAppParam"] = "replaced successfully!";

            // Act
            var labels = await this.RunScenarioAsync();

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
            this.rawServiceManifest =
                $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                  <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                      <Extensions>
                        <Extension Name='IslandGateway'>
                          <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                            <Label Key='IslandGateway.Enable'>true</Label>
                            <Label Key='IslandGateway.foo'>[NonExistingAppParam]</Label>
                          </Labels>
                        </Extension>
                      </Extensions>
                    </StatelessServiceType>
                  </ServiceTypes>
                </ServiceManifest>";

            // Act
            var labels = await this.RunScenarioAsync();

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
        public async void GetExtensionLabels_OverridesEnabled_NewLabelsFromPropertiesGatheredCorrectly()
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
            this.namingServiceProperties =
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
        public async void GetExtensionLabels_OverridesEnabledValueCaseInsensitive_OverridesAreEnabled()
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
            this.namingServiceProperties =
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
        public async void GetExtensionLabels_OverridesEnabled_OnlyIslandGatewayNamespacePropertiesGathered()
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
            this.namingServiceProperties =
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
        public async void GetExtensionLabels_OverridesEnabled_PropertiesOverrideManifestCorrectly()
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
            this.namingServiceProperties =
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
        public async void GetExtensionLabels_OverridesEnabled_LabelKeysAreCaseSensitive()
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
            this.namingServiceProperties =
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
        public async void GetExtensionLabels_OverridesDisabiled_PropertiesNotQueried()
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
            this.namingServiceProperties =
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
        public async void GetExtensionLabels_CaseDifferingLabelKeys_LabelKeysAreCaseSensitive()
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
            this.namingServiceProperties =
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
        public async void GetExtensionLabels_OverridesNotExplicitlyDisabiled_PropertiesNotQueriedByDefault()
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
            this.namingServiceProperties =
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
        public async void GetExtensionLabels_InvalidManifestXml_Throws()
        {
            // Arrange
            this.rawServiceManifest = $@"<this is no xml my man";

            // Act
            Func<Task> func = () => this.RunScenarioAsync();

            // Assert
            await func.Should().ThrowAsync<IslandGatewayConfigException>();
        }

        [Fact]
        public async void GetExtensionLabels_DuplicatedLabelKeysInManifest_ShouldThrowException()
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
            await func.Should().ThrowAsync<IslandGatewayConfigException>();
        }

        [Fact]
        public async void GetExtensionLabels_OverSizeLimit_ShouldThrowException()
        {
            // Arrange
            var longBadString = new string('*', 1024 * 1024);
            this.rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
            <ServiceTypes>
                <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                    <Extensions>
                        <Extension Name='IslandGateway'>
                        <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                             <Label Key='IslandGateway.foo'>'{longBadString}'</Label>
                          </Labels>
                        </Extension>
                    </Extensions>
                </StatelessServiceType>
            </ServiceTypes>
            </ServiceManifest>
            ";

            // Act
            Func<Task> func = () => this.RunScenarioAsync();

            // Assert
            await func.Should().ThrowAsync<IslandGatewayConfigException>();
        }

        [Fact]
        public async void GetExtensionLabels_WithDTD_ShouldThrowException()
        {
            // Arrange
            this.rawServiceManifest =
                $@"
                <!DOCTYPE xxe [
                    <!ENTITY  chybeta  ""Melicious DTD value here."">
                ]>
                < ServiceManifest xmlns='{ServiceFabricExtensionConfigProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='IslandGateway'>
                            <Labels xmlns='{ServiceFabricExtensionConfigProvider.XNSIslandGateway}'>
                                <Label Key='IslandGateway.foo'>bar</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

            // Act
            Func<Task> func = () => this.RunScenarioAsync();

            // Assert
            await func.Should().ThrowAsync<IslandGatewayConfigException>();
        }

        private async Task<Dictionary<string, string>> RunScenarioAsync()
        {
            var configProvider = this.Create<ServiceFabricExtensionConfigProvider>();

            return await configProvider.GetExtensionLabelsAsync(
                application: new ApplicationWrapper
                {
                    ApplicationName = new Uri(ApplicationName),
                    ApplicationTypeName = ApplicationTypeName,
                    ApplicationTypeVersion = ApplicationTypeVersion,
                    ApplicationParameters = this.appParams,
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

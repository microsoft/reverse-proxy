// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.ServiceFabric.Tests;

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
        Mock<ICachedServiceFabricCaller>()
            .Setup(
                m => m.GetServiceManifestName(
                    ApplicationTypeName,
                    ApplicationTypeVersion,
                    ServiceTypeName,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ServiceManifestName);

        Mock<ICachedServiceFabricCaller>()
            .Setup(
                m => m.GetServiceManifestAsync(
                    ApplicationTypeName,
                    ApplicationTypeVersion,
                    ServiceManifestName,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _rawServiceManifest);

        Mock<ICachedServiceFabricCaller>()
            .Setup(
                m => m.EnumeratePropertiesAsync(
                    It.Is<Uri>(name => name.ToString() == ServiceFabricName),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _namingServiceProperties);
    }

    [Fact]
    public async void GetExtensionLabels_UnexistingServiceTypeName_NoLabels()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                  <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='AnotherService'>
                    </StatelessServiceType>
                  </ServiceTypes>
                </ServiceManifest>";

        var labels = await RunScenarioAsync();

        // TODO: consider throwing if the servicetypename does not exist
        labels.Should().Equal(new Dictionary<string, string>());
    }

    [Fact]
    public async void GetExtensionLabels_NoExtensions_NoLabels()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                  <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                    </StatelessServiceType>
                  </ServiceTypes>
                </ServiceManifest>";

        var labels = await RunScenarioAsync();

        labels.Should().Equal(new Dictionary<string, string>());
    }

    [Fact]
    public async void GetExtensionLabels_NoLabelsInExtensions_NoLabels()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                        </Extensions>
                    </StatelessServiceType>
                    <StatelessServiceType ServiceTypeName='AnotherServiceType'>
                        <Extensions>
                            <Extension Name='YARP-preview'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                                <Label Key='YARP.Enable'>true</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

        var labels = await RunScenarioAsync();

        labels.Should().Equal(new Dictionary<string, string>());
    }

    [Fact]
    public async void GetExtensionLabels_NoYarpExtensions_NoLabels()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='AnotherExtension'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                                <Label Key='Bla'>foo</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

        var labels = await RunScenarioAsync();

        labels.Should().Equal(new Dictionary<string, string>());
    }

    [Fact]
    public async void GetExtensionLabels_LabelsInManifestExtensions_GatheredCorrectly()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='YARP-preview'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                                <Label Key='YARP.Enable'>true</Label>
                                <Label Key='YARP.foo'>bar</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

        var labels = await RunScenarioAsync();

        labels.Should().Equal(
            new Dictionary<string, string>
            {
                    { "YARP.Enable", "true" },
                    { "YARP.foo", "bar" },
            });
    }

    [Fact]
    public async void GetExtensionLabels_AppParamReplacements_Replaces()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                  <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                      <Extensions>
                        <Extension Name='YARP-preview'>
                          <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                            <Label Key='YARP.Enable'>true</Label>
                            <Label Key='YARP.foo'>[SomeAppParam]</Label>
                          </Labels>
                        </Extension>
                      </Extensions>
                    </StatelessServiceType>
                  </ServiceTypes>
                </ServiceManifest>";

        _appParams["someAppParam"] = "replaced successfully!";

        var labels = await RunScenarioAsync();

        labels.Should().Equal(
            new Dictionary<string, string>
            {
                    { "YARP.Enable", "true" },
                    { "YARP.foo", "replaced successfully!" },
            });
    }

    [Fact]
    public async void GetExtensionLabels_AppParamReplacements_MissingAppParams_ReplacesWithEmpty()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                  <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                      <Extensions>
                        <Extension Name='YARP-preview'>
                          <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                            <Label Key='YARP.Enable'>true</Label>
                            <Label Key='YARP.foo'>[NonExistingAppParam]</Label>
                          </Labels>
                        </Extension>
                      </Extensions>
                    </StatelessServiceType>
                  </ServiceTypes>
                </ServiceManifest>";

        var labels = await RunScenarioAsync();

        labels.Should().Equal(
            new Dictionary<string, string>
            {
                    { "YARP.Enable", "true" },
                    { "YARP.foo", string.Empty },
            });
    }

    [Fact]
    public async void GetExtensionLabels_MultipleExtensions_OnlyYarpLabelsAreGathered()
    {
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
                            <Extension Name='YARP-preview'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                                <Label Key='YARP.Enable'>true</Label>
                                <Label Key='YARP.routes.route1.Hosts'>example.com</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

        var labels = await RunScenarioAsync();

        labels.Should().Equal(
            new Dictionary<string, string>
            {
                    { "YARP.Enable", "true" },
                    { "YARP.routes.route1.Hosts", "example.com" },
            });
    }

    [Fact]
    public async void GetExtensionLabels_OverridesEnabled_NewLabelsFromPropertiesGatheredCorrectly()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='YARP-preview'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                                <Label Key='YARP.EnableDynamicOverrides'>true</Label>
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
                    { "YARP.Enable", "true" },
                    { "YARP.routes.route1.Hosts", "example.com" },
            };

        var labels = await RunScenarioAsync();

        labels.Should().Equal(
            new Dictionary<string, string>
            {
                    { "YARP.Enable", "true" },
                    { "YARP.routes.route1.Hosts", "example.com" },
                    { "YARP.EnableDynamicOverrides", "true" },
            });
    }

    [Fact]
    public async void GetExtensionLabels_OverridesEnabledValueCaseInsensitive_OverridesAreEnabled()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='YARP-preview'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                                <Label Key='YARP.EnableDynamicOverrides'>True</Label>
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
                    { "YARP.Enable", "true" },
                    { "YARP.routes.route1.Hosts", "example.com" },
            };

        var labels = await RunScenarioAsync();

        labels.Should().Equal(
            new Dictionary<string, string>
            {
                    { "YARP.Enable", "true" },
                    { "YARP.routes.route1.Hosts", "example.com" },
                    { "YARP.EnableDynamicOverrides", "True" },
            });
    }

    [Fact]
    public async void GetExtensionLabels_OverridesEnabled_OnlyYarpNamespacePropertiesGathered()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='YARP-preview'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                                <Label Key='YARP.EnableDynamicOverrides'>true</Label>
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
                    { "YARP.Enable", "true" },
                    { "YARp.Enable", "false" },
                    { "WhatIsThisNamespace.value", "42" },
            };

        var labels = await RunScenarioAsync();

        labels.Should().Equal(
            new Dictionary<string, string>
            {
                    { "YARP.Enable", "true" },
                    { "YARP.EnableDynamicOverrides", "true" },
            });
    }

    [Fact]
    public async void GetExtensionLabels_OverridesEnabled_PropertiesOverrideManifestCorrectly()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='YARP-preview'>
                             <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                                <Label Key='YARP.Enable'>true</Label>
                                <Label Key='YARP.EnableDynamicOverrides'>true</Label>
                                <Label Key='YARP.routes.route1.Hosts'>example.com</Label>
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
                    { "YARP.Enable", "false" },
            };

        var labels = await RunScenarioAsync();

        labels.Should().Equal(
            new Dictionary<string, string>
            {
                    { "YARP.Enable", "false" },
                    { "YARP.routes.route1.Hosts", "example.com" },
                    { "YARP.EnableDynamicOverrides", "true" },
            });
    }

    [Fact]
    public async void GetExtensionLabels_OverridesEnabled_LabelKeysAreCaseSensitive()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='YARP-preview'>
                             <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                                <Label Key='YARP.Enable'>true</Label>
                                <Label Key='YARP.EnableDynamicOverrides'>true</Label>
                                <Label Key='YARP.routes.route1.Hosts'>example.com</Label>
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
                    { "YARP.Routes.route1.HOST", "another.example.com" },
            };

        var labels = await RunScenarioAsync();

        labels.Should().Equal(
            new Dictionary<string, string>
            {
                    { "YARP.Enable", "true" },
                    { "YARP.routes.route1.Hosts", "example.com" },
                    { "YARP.EnableDynamicOverrides", "true" },
                    { "YARP.Routes.route1.HOST", "another.example.com" },
            });
    }

    [Fact]
    public async void GetExtensionLabels_OverridesDisabiled_PropertiesNotQueried()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='YARP-preview'>
                             <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                                <Label Key='YARP.Enable'>true</Label>
                                <Label Key='YARP.EnableDynamicOverrides'>false</Label>
                                <Label Key='YARP.routes.route1.host'>example.com</Label>
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
                    { "YARP.Enable", "false" },
            };

        var labels = await RunScenarioAsync();

        labels.Should().Equal(
            new Dictionary<string, string>
            {
                    { "YARP.Enable", "true" },
                    { "YARP.routes.route1.host", "example.com" },
                    { "YARP.EnableDynamicOverrides", "false" },
            });

        Mock<ICachedServiceFabricCaller>().Verify(m => m.EnumeratePropertiesAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async void GetExtensionLabels_CaseDifferingLabelKeys_LabelKeysAreCaseSensitive()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='YARP-preview'>
                             <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                                <Label Key='YARP.Enable'>true</Label>
                                <Label Key='YARP.EnableDynamicOverrides'>true</Label>
                                <Label Key='YARP.routes.ROUTE1.Hosts'>example.com</Label>
                                <Label Key='YARP.routes.route1.hosts'>another.example.com</Label>
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
                    { "YARP.routes.Route1.HOSTS", "etc.example.com" },
            };

        var labels = await RunScenarioAsync();

        labels.Should().Equal(
            new Dictionary<string, string>
            {
                    { "YARP.Enable", "true" },
                    { "YARP.routes.ROUTE1.Hosts", "example.com" },
                    { "YARP.routes.route1.hosts", "another.example.com" },
                    { "YARP.routes.Route1.HOSTS", "etc.example.com" },
                    { "YARP.EnableDynamicOverrides", "true" },
            });
    }

    [Fact]
    public async void GetExtensionLabels_OverridesNotExplicitlyDisabiled_PropertiesNotQueriedByDefault()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='YARP-preview'>
                             <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                                <Label Key='YARP.Enable'>true</Label>
                                <Label Key='YARP.routes.route1.host'>example.com</Label>
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
                    { "YARP.Enable", "false" },
            };

        var labels = await RunScenarioAsync();

        labels.Should().Equal(
            new Dictionary<string, string>
            {
                    { "YARP.Enable", "true" },
                    { "YARP.routes.route1.host", "example.com" },
            });

        Mock<ICachedServiceFabricCaller>().Verify(m => m.EnumeratePropertiesAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async void GetExtensionLabels_InvalidManifestXml_Throws()
    {
        _rawServiceManifest = $@"<this is no xml my man";

        Func<Task> func = () => RunScenarioAsync();

        await func.Should().ThrowAsync<ConfigException>();
    }

    [Fact]
    public async void GetExtensionLabels_DuplicatedLabelKeysInManifest_ShouldThrowException()
    {
        _rawServiceManifest =
            $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatefulService ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                             <Extension Name='YARP-preview'>
                             <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                                <Label Key='YARP.Enable'>true</Label>
                                <Label Key='YARP.routes.route1.host'>example.com</Label>
                                <Label Key='YARP.routes.route1.host'>example.com</Label>
                             </Labels>
                             </Extension>
                        </Extensions>
                    </StatefulService>
                </ServiceTypes>
                </ServiceManifest>
                ";

        Func<Task> func = () => RunScenarioAsync();

        await func.Should().ThrowAsync<ConfigException>();
    }

    [Fact]
    public async void GetExtensionLabels_OverSizeLimit_ShouldThrowException()
    {
        var longBadString = new string('*', 1024 * 1024);
        _rawServiceManifest =
        $@"<ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
            <ServiceTypes>
                <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                    <Extensions>
                        <Extension Name='YARP-preview'>
                        <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                             <Label Key='YARP.foo'>'{longBadString}'</Label>
                          </Labels>
                        </Extension>
                    </Extensions>
                </StatelessServiceType>
            </ServiceTypes>
            </ServiceManifest>
            ";

        Func<Task> func = () => RunScenarioAsync();

        await func.Should().ThrowAsync<ConfigException>();
    }

    [Fact]
    public async void GetExtensionLabels_WithDTD_ShouldThrowException()
    {
        _rawServiceManifest =
            $@"
                <!DOCTYPE xxe [
                    <!ENTITY  chybeta  ""Melicious DTD value here."">
                ]>
                < ServiceManifest xmlns='{ServiceExtensionLabelsProvider.XNSServiceManifest}'>
                <ServiceTypes>
                    <StatelessServiceType ServiceTypeName='{ServiceTypeName}'>
                        <Extensions>
                            <Extension Name='YARP-preview'>
                            <Labels xmlns='{ServiceExtensionLabelsProvider.XNSFabricNoSchema}'>
                                <Label Key='YARP.foo'>bar</Label>
                            </Labels>
                            </Extension>
                        </Extensions>
                    </StatelessServiceType>
                </ServiceTypes>
                </ServiceManifest>
                ";

        Func<Task> func = () => RunScenarioAsync();

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

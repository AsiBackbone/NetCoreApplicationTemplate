using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ProjectTemplate.Web.Extensions;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class DataProtectionServiceExtensionsTests
{
    [Fact]
    public void SharedKeyRingAndApplicationName_AllowCrossInstancePayloadRoundTrip()
    {
        string contentRootPath = CreateTemporaryDirectory();

        try
        {
            using ServiceProvider firstInstance = CreateServiceProvider(contentRootPath, "SharedApplication");
            IDataProtector firstProtector = firstInstance
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("cross-instance-test");
            string protectedPayload = firstProtector.Protect("payload");

            using ServiceProvider secondInstance = CreateServiceProvider(contentRootPath, "SharedApplication");
            IDataProtector secondProtector = secondInstance
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("cross-instance-test");

            Assert.Equal("payload", secondProtector.Unprotect(protectedPayload));
            Assert.NotEmpty(Directory.GetFiles(Path.Combine(contentRootPath, "keys"), "key-*.xml"));
        }
        finally
        {
            Directory.Delete(contentRootPath, recursive: true);
        }
    }

    [Fact]
    public void DifferentApplicationName_IsolatesProtectedPayloads()
    {
        string contentRootPath = CreateTemporaryDirectory();

        try
        {
            using ServiceProvider firstInstance = CreateServiceProvider(contentRootPath, "FirstApplication");
            IDataProtector firstProtector = firstInstance
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("application-isolation-test");
            string protectedPayload = firstProtector.Protect("payload");

            using ServiceProvider secondInstance = CreateServiceProvider(contentRootPath, "SecondApplication");
            IDataProtector secondProtector = secondInstance
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("application-isolation-test");

            _ = Assert.Throws<CryptographicException>(() => secondProtector.Unprotect(protectedPayload));
        }
        finally
        {
            Directory.Delete(contentRootPath, recursive: true);
        }
    }

    [Theory]
    [InlineData("ProjectTemplate:DataProtection:ApplicationName")]
    [InlineData("ProjectTemplate:DataProtection:KeyRingPath")]
    public void BlankRequiredSetting_ThrowsInvalidOperationException(string settingName)
    {
        string contentRootPath = CreateTemporaryDirectory();

        try
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [settingName] = " "
                })
                .Build();
            ServiceCollection services = new();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                services.AddApplicationDataProtection(configuration, new TestHostEnvironment(contentRootPath)));

            Assert.Contains($"{settingName} is required.", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(contentRootPath, recursive: true);
        }
    }

    private static ServiceProvider CreateServiceProvider(string contentRootPath, string applicationName)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ApplicationDataProtectionOptions.SectionName}:ApplicationName"] = applicationName,
                [$"{ApplicationDataProtectionOptions.SectionName}:KeyRingPath"] = "keys"
            })
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddApplicationDataProtection(configuration, new TestHostEnvironment(contentRootPath));

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"projecttemplate-data-protection-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "ProjectTemplate.Web.Tests";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

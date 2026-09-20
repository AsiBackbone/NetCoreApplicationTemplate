using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
    public void BlankRequiredSetting_ThrowsOptionsValidationException(string settingName)
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
            services.AddLogging();
            services.AddApplicationDataProtection(configuration, new TestHostEnvironment(contentRootPath));

            using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

            OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
                _ = provider.GetRequiredService<IOptions<ApplicationDataProtectionOptions>>().Value);

            Assert.Contains($"{settingName} is required.", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(contentRootPath, recursive: true);
        }
    }

    [Fact]
    public void KeyEncryptionCertificate_EncryptsKeyRingAndAllowsCrossInstanceRoundTrip()
    {
        string contentRootPath = CreateTemporaryDirectory();

        try
        {
            string certificatePath = CreateKeyEncryptionCertificate(contentRootPath, "certificate-password");
            Dictionary<string, string?> certificateSettings = new()
            {
                [$"{ApplicationDataProtectionOptions.SectionName}:KeyEncryptionCertificatePath"] = certificatePath,
                [$"{ApplicationDataProtectionOptions.SectionName}:KeyEncryptionCertificatePassword"] = "certificate-password"
            };

            string protectedPayload;

            using (ServiceProvider firstInstance = CreateServiceProvider(contentRootPath, "EncryptedApplication", certificateSettings))
            {
                protectedPayload = firstInstance
                    .GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("encrypted-key-ring-test")
                    .Protect("payload");
            }

            string keyFile = Assert.Single(Directory.GetFiles(Path.Combine(contentRootPath, "keys"), "key-*.xml"));
            string keyXml = File.ReadAllText(keyFile);

            Assert.Contains("EncryptedData", keyXml, StringComparison.Ordinal);
            Assert.DoesNotContain("<masterKey", keyXml, StringComparison.Ordinal);

            using ServiceProvider secondInstance = CreateServiceProvider(contentRootPath, "EncryptedApplication", certificateSettings);
            IDataProtector secondProtector = secondInstance
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("encrypted-key-ring-test");

            Assert.Equal("payload", secondProtector.Unprotect(protectedPayload));
        }
        finally
        {
            Directory.Delete(contentRootPath, recursive: true);
        }
    }

    [Fact]
    public void KeyEncryptionCertificate_MissingFile_ThrowsInvalidOperationException()
    {
        string contentRootPath = CreateTemporaryDirectory();

        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                CreateServiceProvider(
                    contentRootPath,
                    "MissingCertificateApplication",
                    new Dictionary<string, string?>
                    {
                        [$"{ApplicationDataProtectionOptions.SectionName}:KeyEncryptionCertificatePath"] = "missing.pfx"
                    }));

            Assert.Contains("was not found", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(contentRootPath, recursive: true);
        }
    }

    [Fact]
    public void KeyEncryptionCertificatePasswordWithoutPath_ThrowsOptionsValidationException()
    {
        string contentRootPath = CreateTemporaryDirectory();

        try
        {
            using ServiceProvider provider = CreateServiceProvider(
                contentRootPath,
                "PasswordOnlyApplication",
                new Dictionary<string, string?>
                {
                    [$"{ApplicationDataProtectionOptions.SectionName}:KeyEncryptionCertificatePassword"] = "orphaned-password"
                });

            OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
                _ = provider.GetRequiredService<IOptions<ApplicationDataProtectionOptions>>().Value);

            Assert.Contains("requires KeyEncryptionCertificatePath", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(contentRootPath, recursive: true);
        }
    }

    private static ServiceProvider CreateServiceProvider(
        string contentRootPath,
        string applicationName,
        IReadOnlyDictionary<string, string?>? additionalSettings = null)
    {
        Dictionary<string, string?> settings = new()
        {
            [$"{ApplicationDataProtectionOptions.SectionName}:ApplicationName"] = applicationName,
            [$"{ApplicationDataProtectionOptions.SectionName}:KeyRingPath"] = "keys"
        };

        foreach (KeyValuePair<string, string?> setting in additionalSettings ?? new Dictionary<string, string?>())
        {
            settings[setting.Key] = setting.Value;
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddApplicationDataProtection(configuration, new TestHostEnvironment(contentRootPath));

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static string CreateKeyEncryptionCertificate(string directory, string password)
    {
        using var rsa = RSA.Create(2048);
        CertificateRequest request = new(
            "CN=ProjectTemplate Data Protection Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30));

        string certificatePath = Path.Combine(directory, "data-protection-test.pfx");
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx, password));

        return certificatePath;
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

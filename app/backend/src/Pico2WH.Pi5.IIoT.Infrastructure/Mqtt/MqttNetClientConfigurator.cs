using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet.Client;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Mqtt;

/// <summary>建立 MQTTnet 連線選項（發布與訂閱共用 TLS／憑證邏輯）。</summary>
public static class MqttNetClientConfigurator
{
    public static MqttClientOptions BuildClientOptions(
        MqttOptions options,
        IHostEnvironment hostEnvironment,
        string clientId,
        ILogger logger)
    {
        var mqttOptionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(options.Host, options.Port)
            .WithClientId(clientId);

        if (!string.IsNullOrEmpty(options.Username))
            mqttOptionsBuilder = mqttOptionsBuilder.WithCredentials(options.Username, options.Password ?? string.Empty);

        var useTls = options.UseTls || options.Port == 8883;
        if (useTls)
        {
            mqttOptionsBuilder = mqttOptionsBuilder.WithTlsOptions(tls =>
            {
                tls.UseTls();
                tls.WithSslProtocols(System.Security.Authentication.SslProtocols.Tls12 |
                                     System.Security.Authentication.SslProtocols.Tls13);

                var handler = BuildCertificateValidationHandler(options, hostEnvironment, logger);
                if (handler is not null)
                    tls.WithCertificateValidationHandler(handler);

                var clientCerts = LoadClientCertificates(options, hostEnvironment, logger);
                if (clientCerts.Count > 0)
                    tls.WithClientCertificates(clientCerts);
            });
        }

        return mqttOptionsBuilder.Build();
    }

    private static Func<MqttClientCertificateValidationEventArgs, bool>? BuildCertificateValidationHandler(
        MqttOptions options,
        IHostEnvironment hostEnvironment,
        ILogger logger)
    {
        if (options.TlsAllowUntrustedCertificate)
            return _ => true;

        var caPath = ResolvePath(options.TlsCaCertificatePath, hostEnvironment);
        if (string.IsNullOrEmpty(caPath) || !File.Exists(caPath))
        {
            logger.LogWarning("MQTT TLS：未設定有效之 TlsCaCertificatePath，將依系統預設驗證憑證。path={Path}", caPath);
            return null;
        }

        try
        {
            var ca = new X509Certificate2(caPath);
            return args =>
            {
                try
                {
                    using var chain = new X509Chain();
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    chain.ChainPolicy.CustomTrustStore.Add(ca);
                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    using var server = new X509Certificate2(args.Certificate);
                    return chain.Build(server);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "MQTT TLS 憑證驗證失敗");
                    return false;
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "無法載入 MQTT CA 憑證：{Path}", caPath);
            return _ => false;
        }
    }

    private static List<X509Certificate2> LoadClientCertificates(
        MqttOptions options,
        IHostEnvironment hostEnvironment,
        ILogger logger)
    {
        var certPath = ResolvePath(options.TlsClientCertificatePath, hostEnvironment);
        var keyPath = ResolvePath(options.TlsClientPrivateKeyPath, hostEnvironment);
        if (string.IsNullOrEmpty(certPath) || !File.Exists(certPath))
            return new List<X509Certificate2>();

        if (string.IsNullOrEmpty(keyPath) || !File.Exists(keyPath))
        {
            logger.LogWarning("已設定 TlsClientCertificatePath 但缺少對應私鑰路徑");
            return new List<X509Certificate2>();
        }

        try
        {
            var pemCert = File.ReadAllText(certPath);
            var pemKey = File.ReadAllText(keyPath);
            var pair = X509Certificate2.CreateFromPem(pemCert, pemKey);
            return new List<X509Certificate2> { pair };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "無法載入 MQTT 客戶端憑證");
            return new List<X509Certificate2>();
        }
    }

    private static string? ResolvePath(string? relativeOrAbsolute, IHostEnvironment hostEnvironment)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
            return null;

        if (Path.IsPathRooted(relativeOrAbsolute))
            return relativeOrAbsolute;

        return Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, relativeOrAbsolute));
    }
}

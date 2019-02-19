using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace AzureWebJobsCertificateUpdater
{
    /*
     * 環境変数から`CertificateUpdater:`もしくは`CertificateUpdater__`プレフィクスで始まる各プロパティ名の値を読み取ります。
     * See: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-2.2#environment-variables-configuration-provider
     */
    public class AppSettings
    {
        private IConfigurationSection _config;

        public string Domain => GetNotNullValue<string>(nameof(Domain));

        public string KeyVaultId => GetNotNullValue<string>(nameof(KeyVaultId));
        public string CertificateName => GetNotNullValue<string>(nameof(CertificateName));
        public bool ForceUpdate => GetNotNullValue(nameof(ForceUpdate), false);

        public static AppSettings CertificateUpdater = new AppSettings();

        private T GetNotNullValue<T>(string key, T defaultValue = default(T))
        {
            T value = this._config.GetValue<T>(key, defaultValue);
            if (value == null)
            {
                throw new KeyNotFoundException(string.Format("Configuration Key was not found. (CertificateUpdater:{0})", nameof(key)));
            }
            return value;
        }

        private AppSettings()
        {

            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddEnvironmentVariables();

            var configuration = configurationBuilder.Build();

            this._config = configuration.GetSection("CertificateUpdater");
        }
    }
}
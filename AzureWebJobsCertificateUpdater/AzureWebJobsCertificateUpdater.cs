using System;
using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using System.Linq;
using Microsoft.Rest;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace AzureWebJobsCertificateUpdater
{
    class AzureWebJobsCertificateUpdater
    {
        public static void Main(string[] args) => MainAsync(args).Wait();

        public static async Task MainAsync(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // WebApp Environment
            var WEBSITE_OWNER_NAME = GetEnvironment("WEBSITE_OWNER_NAME");
            var SUBSCRIPTION_ID = WEBSITE_OWNER_NAME.Split('+')[0];
            var WEBSITE_RESOURCE_GROUP = GetEnvironment("WEBSITE_RESOURCE_GROUP");
            var WEBSITE_SITE_NAME = GetEnvironment("WEBSITE_SITE_NAME");

            // AppSettings "CertificationUpdater:xxx"
            var DOMAINS = AppSettings.CertificateUpdater.Domain.Split(",");
            var KEYVAULT_ID = AppSettings.CertificateUpdater.KeyVaultId;
            var KEYVAULT_CERTIFICATE_NAME = AppSettings.CertificateUpdater.CertificateName;
            var IS_FORCE_UPDATE = AppSettings.CertificateUpdater.ForceUpdate;

            // Using Managed ID
            var tokenProvider = new AzureServiceTokenProvider();
            var token = await tokenProvider.GetAccessTokenAsync("https://management.azure.com/");

            var webSiteManagementClient = new WebSiteManagementClient(new TokenCredentials(token))
            {
                SubscriptionId = SUBSCRIPTION_ID
            };

            // Slotは未サポート
            var webSite = webSiteManagementClient.WebApps.Get(WEBSITE_RESOURCE_GROUP, WEBSITE_SITE_NAME);

            if (webSite == null)
            {
                // WEBSITE_SITE_NAMEが何らかの理由で間違えていると、例外にならずnullが返ってくる。（WebApp側が設定するので間違えるはずはないのだけど。）
                // しかし、後続の処理で引っかかるのが嫌なのでチェックする。
                throw new Exception($"Invalid parameters or not found webapp. SubscriptionId='{SUBSCRIPTION_ID}', ResouceGroup='{WEBSITE_RESOURCE_GROUP}', Name='{WEBSITE_SITE_NAME}'");
            }

            foreach(var domain in DOMAINS){

                if (!IS_FORCE_UPDATE && NeedCreateOrUpdate(webSiteManagementClient, WEBSITE_RESOURCE_GROUP, WEBSITE_SITE_NAME, domain))
                {
                    // 更新不要
                    return;
                }

                // KeyVaultへの参照を行い、最新の証明書の情報を得る
                var certificate = webSiteManagementClient.Certificates.CreateOrUpdate(WEBSITE_RESOURCE_GROUP, WEBSITE_SITE_NAME, new Certificate()
                {
                    Location = webSite.Location,
                    ServerFarmId = webSite.ServerFarmId,
                    KeyVaultId = KEYVAULT_ID,
                    KeyVaultSecretName = KEYVAULT_CERTIFICATE_NAME,
                    Password = "", // TODO: PassPhraseが必要になったら改修する
                });

                // カスタムドメインの作成と証明書の紐づけを兼ねている
                HostNameBinding result = webSiteManagementClient.WebApps.CreateOrUpdateHostNameBinding(WEBSITE_RESOURCE_GROUP, WEBSITE_SITE_NAME, domain, new HostNameBinding()
                {
                    Thumbprint = certificate.Thumbprint,
                    SslState = SslState.SniEnabled,
                });

                // Done
                Console.WriteLine($"更新しました。domain={domain}, ExpirationDate={certificate.ExpirationDate}, Thumbprint='{result.Thumbprint}'");
            }
        }

        /*
         * 証明書を作成or更新する必要があるかどうかチェックします。
         */
        public static bool NeedCreateOrUpdate(WebSiteManagementClient webSiteManagementClient, string websiteResourceGroup, string websiteSiteName, string certificateDomain)
        {
            try
            {
                var hostNameBinding = webSiteManagementClient.WebApps.GetHostNameBinding(websiteResourceGroup, websiteSiteName, certificateDomain);
                if (hostNameBinding == null && hostNameBinding.Thumbprint == null)
                {
                    // 「カスタムドメインを設定しているが、SSL設定をしていない（紐づく証明書が無い）場合。
                    // 後続のCreateOrUpdateHostNameBindingで証明書の紐づけが行えるので、このまま進める。
                    return true;
                }

                // TODO: 1つのWebAppに複数証明書が紐づくことはあるのか設定上は出来そうな気がするが、これで大丈夫か不明。（複数ドメイン使った場合とか。）
                Certificate currentCertificate = webSiteManagementClient.Certificates.Get(websiteResourceGroup, websiteSiteName);

                // 紐づいている証明書の有効期限を確認する
                if (currentCertificate != null && currentCertificate.ExpirationDate.HasValue && hostNameBinding.Thumbprint == currentCertificate.Thumbprint)
                {
                    TimeSpan span = currentCertificate.ExpirationDate.Value - DateTime.Now;
                    if (span.Days > 30)
                    {
                        Console.WriteLine($"有効期限切れまであと{span.Days}日あります。更新はスキップします。");
                        return false;
                    }
                }

                // 有効期限n日前なので更新する
                return true;
            }
            catch (System.Exception e)
            {
                if (e.Message.IndexOf("Operation returned an invalid status code 'NotFound'") != -1)
                {
                    // この例外はまだカスタムドメインが無いので新規作成すべき。
                    // 後続のCreateOrUpdateHostNameBindingでカスタムドメインの新規作成と証明書の紐づけが行えるので、このまま進める。
                    return true;
                }
                else
                {
                    // それ以外は何か問題があるので、例外扱いにする
                    throw e;
                }
            }
        }

        public static string GetEnvironment(string key, string defaultValue = null)
        {
            var value = Environment.GetEnvironmentVariable(key);

            if (string.IsNullOrEmpty(value))
            {
                if (defaultValue != null)
                {
                    return defaultValue;
                }

                throw new KeyNotFoundException($"Environment.GetEnvironmentVariable('{key}') is null or empty string.");
            }

            return value;
        }
    }
}

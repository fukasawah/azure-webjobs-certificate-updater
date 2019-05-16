Azure WebJobs Certificate Updater
=================================

以下を行います

- KeyVaultの証明書を使い、WebAppsのSSL設定を行う
    - 有効期限の30日前は何もしない
- もしカスタムドメインとSSL設定が未設定の場合は行う
- ワイルドカード証明書であれば同一サイト複数ドメインで設定可能

SSL設定を行うため、Basic以上のプランである必要があります。

以下はサポートしていません

- スロット
- Passphrase付き秘密鍵（必要になったら改修予定）
- 更新時の通知


導入手順
---------------------------------

### WebAppのマネージドID(旧:マネージドサービスID(MSI))を有効にする

Portal上からWebApp→Identity→System Assignedタブ→StatusをOnにします。

これにより、このWebApp用のシステム割り当てのマネージドIDが生成されます。（WebApp名で見つけられるようになります）

### マネージドIDにリソースグループのアクセス制御を与える

WebAppの**リソースグループのアクセス制御(IAM)**を開きます。**「WebApp」ではなく「リソースグループ」に対して行う点に注意してください。**


以下のロールと以下のメンバーでアクセス許可を行います。

- `Role`: `WebSite Contributor` (Webサイト 共同作成者)
- `Member`: WebApp名 （出てこない場合、マネージドIDが有効になっていません）

その後、Saveします。


### マネージドIDにKeyVaultのアクセス制御を与える

証明書を格納しているKeyVaultを開き、アクセス制御(IAM)を開きます。（アクセスポリシーではありません）

以下のロールと以下のメンバーでアクセス許可を行います。

- `Role`: `KeyVault Contributor` (Key Vault 共同作成者)
- `Member`: WebApp名

その後、Saveします。

### KeyVaultのアクセスポリシーに`Microsoft Azure App Service`を登録

KeyVaultを開き、Access Policy→Add Newで新規のポリシーを追加します。

Select Principalにて、「Microsoft Azure App Service」または「`abfa0a7c-a6b6-4736-8310-5855508787cd`」を入力して選択します。

権限は`Secret`の`Get`のみ与えて登録します。


### 環境変数（アプリケーション設定）を設定する

WebAppのApplication Settingsにて、以下の環境変数を設定します。

| 変数名 | 意味 | 例 |
|---|---|---|
|`CertificateUpdater:Domain`| WebAppのSSL証明書をバインドするHostName(ドメイン名) カンマ区切りで複数指定可能 | `foo.example.jp,bar.example.jp` |
|`CertificateUpdater:KeyVaultId`| 証明書が格納されているKeyVaultのリソースID | `/subscriptions/サブスクリプションID/resourceGroups/リソースグループ名/providers/Microsoft.KeyVault/vaults/KeyVault名` |
|`CertificateUpdater:CertificateName`| KeyVaultに格納されている証明書の名前 | `foo-example-jp` |
|`CertificateUpdater:ForceUpdate`| (Optional) 有効期限にかかわらず強制的に更新する。デフォルト:`false` | `false` or `true` |

この環境変数の読み取りの仕組みについては、「
[Environment Variables Configuration Provider - Configuration in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-2.2#environment-variables-configuration-provider)」に倣っています。


### WebJobsのスクリプトをアップロードする

配布しているzipファイルをWebJobsとして登録します。

種類は「トリガー」、スケールは「単一インスタンス」で登録することを推奨します。


ビルド手順
------------------------

### 準備

予め、`.Net Core SDK`を導入しておき、`dotnet`コマンドを利用できる状態にしておく必要があります。

### ビルド

```
dotnet restore
dotnet publish -c Release
```

### 配布用ファイルの作成

`AzureWebJobsCertificateUpdater/bin/Release/netcoreapp2.2` 辺りにファイルが配置されるため、ファイルをすべて選択してzip圧縮を行います。
※`netcoreapp2.2`ディレクトリを圧縮しないように注意してください。

出来上がったzipファイルをwebjobsとして登録することができます。



その他
------------------

### 利用している他の環境変数について

以下の環境変数も扱いますが、内部で定義されるため意識する必要はありませんが、手元からのデバッグ実行したい場合などに必要になるため記載します。

| 変数名 | 意味 | 例 |
|---|---|---|
|`WEBSITE_OWNER_NAME`|  | `サブスクリプションID+WebApp名-リージョン名WebSpace` |
|`WEBSITE_RESOURCE_GROUP`| WebAppが動作しているリソースグループ名| - |
|`WEBSITE_SITE_NAME`| WebApp名 | - |



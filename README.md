# WioLinkApp

## WioLinkCLI

### 使い方

`wio <コマンド>`

|コマンド|説明|
|:--|:--|
|prov|デバイスをプロビジョニングします。|

## wio prov

デバイスをプロビジョニングします。

### オプション

* -n, --name (必須)

  デバイスの名前です。

* -s, --server (必須)

  接続するWioサーバーです。

* -u, --user (必須)

  Wioサーバーにログインするユーザー名です。

* -p, --password

  Wioサーバーにログインするパスワードです。

* -S, --wifi-ssid (必須)

  デバイスに設定するWi-FiのSSIDです。

* -P, --wifi-password

  デバイスに設定するWi-Fiのパスワードです。

### コマンド例

```
wio prov -n node1 -s wiolink.seeed.co.jp -u user@domain -p password -S ssid -P password
```

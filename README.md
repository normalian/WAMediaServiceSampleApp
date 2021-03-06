# Windows Azure メディアサービス用のサンプルアプリケーション
Windows Azure メディアサービスを利用するためのサンプルアプリケーションです。
本アプリケーションの概要情報は [SlieShare - Windows Azure Media Serviceで作成する割と普通な動画サイト](http://www.slideshare.net/normalian/windows-azure-media-service-17064430 "Windows Azure Media Serviceで作成する割と普通な動画サイト") を参照してください。

### 本サンプルに必要なもの
* H.264 形式（*.mp4）の動画が必要です
* Windows Azure のサブスクリプション契約を実施して下さい

### 利用手順メモ
1. git clone で本サンプルアプリケーションを取得してください
2. Windows Azure 管理ポータルから、利用するメディアサービスの**アカウント名**、**アクセス・キー**を取得してください
3. メディアサービスにアップロードする動画を任意のファイルパス上に配置してください
4. 「App.config の値を編集」を参考に、必要な情報を編集してください。
5. （必要な場合）ソリューションエクスプローラから「NuGetパッケージの復元の有効化」を選択し、パッケージの参照を回復してください
6. アプリケーションを実行します

### App.config の値を編集
以下の値を修正する必要があります。

 * AccountName
   * 管理ポータルから取得したメディアサービスのアカウント名を入力してください
 * AccountKey
   * 管理ポータルから取得したメディアサービスのキー名を入力してください
 * MovieFilePath 
   * H.264（*.mp4）形式の動画ファイルのパスを指定してください
 * UrlFilePath
   * メディアサービス側でエンコーディング、公開したURLを書き込みます。ファイルの書き込み権限が存在するファイルパスを指定してください。

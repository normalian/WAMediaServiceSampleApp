# Windows Azure Media Service 用のサンプルアプリケーション
Windows Azure Media Service を利用するためのサンプルアプリケーションです。
本アプリケーションの概要情報は [SlieShare - Windows Azure Media Serviceで作成する割と普通な動画サイト](http://www.slideshare.net/normalian/windows-azure-media-service-17064430 "Windows Azure Media Serviceで作成する割と普通な動画サイト") を参照してください。

## 利用手順
本プロジェクトを git clone 等で取得した後、以下の対応を実施してください

### 動画ファイルの作成
*.mp4 形式の動画を作成 or 入手してください

### App.config の値を編集
以下の値を修正してください。
 * _accountName: "＜メディアサービスアカウント名＞";
   * 自身のメディアサービスのアカウント名を入力してください
 * _accountKey: "＜メディアサービスキー名＞";
   * 自身のメディアサービスのキー名を入力してください
 * moviefilePath: @"＜動画のパス＞.mp4";
   * mp4 形式の動画ファイルのパスを指定してください
 * urlfilePath: @"C:\Temp\SasUrlList.txt";
   * "C:\Temp" フォルダは事前に作成してください

### アプリケーションの実行
Visual Studio 等でアプリケーションを実行します
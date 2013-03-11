﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure.MediaServices.Client;

namespace MediaConsoleApp
{
    class Program
    {
        //Media Service のアカウント名とキー名
        private static string _accountName = "＜メディアサービスアカウント名＞";
        private static string _accountKey = "＜メディアサービスキー名＞";

        //適宜変更して欲しいパス名
        private static string moviefilePath = @"＜動画のパス＞.mp4";
        private static string urlfilePath = @"C:\Temp\SasUrlList.txt";

        static void Main(string[] args)
        {
            //管理ポータルに表示される
            string assetName = "某店舗動画";

            Console.WriteLine("------------ Application Start ------------");

            CloudMediaContext context = new CloudMediaContext(_accountName, _accountKey);

            //① 動画のアップロード（※注 エンコード完了には時間がかかります）
            Console.WriteLine("①動画のアップロード～公開迄を実施");
            UploadSimpleAsset(context, assetName, moviefilePath);
            EncodeSimpleAsset(context, assetName);
            PublishSimpleAsset(context, assetName, urlfilePath);

            //② 既存のアセットに対するサムネイルの作成＆公開（※注 エンコード完了には時間がかかります）
            Console.WriteLine("②アップロードした動画からサムネイルを生成＆公開");
            CreateThumbnails(context, assetName);
            PublishThumbnails(context, assetName, urlfilePath);

            Console.WriteLine("------------ Application End   ------------");
            Console.ReadLine();
        }

        #region 動画アップロード → 公開までのサンプルコード
        private static void UploadSimpleAsset(CloudMediaContext context, string assetName, string moviefilePath)
        {
            // アセットのインスタンスを作成、ストレージ暗号化はしない
            var asset = context.Assets.Create(assetName,
                AssetCreationOptions.None);

            // ファイル名からアセットファイルを作成する
            var assetFile = asset.AssetFiles.Create(Path.GetFileName(moviefilePath));

            // アップロード進捗を確認するためのハンドラ追加
            assetFile.UploadProgressChanged += (sender, e) =>
                Console.WriteLine("★ {0}% uploaded. {1}/{2} bytes",
                e.Progress,
                e.BytesUploaded,
                e.TotalBytes);

            Console.WriteLine("\tアップロード開始");
            //アップロードのメソッドは非同期版(UploadAsync)も存在
            assetFile.Upload(moviefilePath);
            Console.WriteLine("\tアップロード終了");
        }

        static void AssetFile_UploadProgressChanged(object sender, UploadProgressChangedEventArgs e)
        {
            //現在のファイルアップロード状況を確認
            Console.WriteLine("★ {0}% uploaded. {1}/{2} bytes", e.Progress, e.BytesUploaded, e.TotalBytes);
        }

        private static void EncodeSimpleAsset(CloudMediaContext context, string assetName)
        {
            var asset = context.Assets.Where(_ => _.Name == assetName).ToList()[0];

            //ジョブの作成
            Console.WriteLine("\tジョブの作成を開始");
            var job = context.Jobs.Create("動画 Encoding Job");
            var task = job.Tasks.AddNew("動画 Encoding Task",
                GetMediaProcessor("Windows Azure Media Encoder", context),
                "H264 Broadband SD 4x3",
                // ここの引数を以下のMSDNを参考に変更することで、エンコードを変更可能
                // http://msdn.microsoft.com/en-us/library/windowsazure/jj129582.aspx
                TaskOptions.None);
            task.InputAssets.Add(asset);
            task.OutputAssets.AddNew(assetName + " - H264 Broadband SD 4x3", AssetCreationOptions.None);

            //ジョブの実行
            Console.WriteLine("\tジョブの実行");
            job.Submit();
        }

        private static void PublishSimpleAsset(CloudMediaContext context, string assetName, string urlfilePath)
        {
            var asset = context.Assets.Where(_ => _.Name == assetName).ToList()[0];

            //一つのアセットに割り当てられるlocatorは10個までなので、古いlocator情報を削除
            Console.WriteLine("\t古いlocatorを削除");
            foreach (var oldlocator in asset.Locators)
            {
                oldlocator.Delete();
            }

            //Locator の割り当て
            Console.WriteLine("\t公開用Locatorの割り当て");
            IAccessPolicy accessPolicy =
                context.AccessPolicies.Create("30日読みとり許可", TimeSpan.FromDays(30), AccessPermissions.Read);
            ILocator locator =
                context.Locators.CreateLocator(LocatorType.Sas, asset, accessPolicy, DateTime.UtcNow.AddDays(-1));

            //表示用の動画ファイルのURL格納先
            string outFilePath = Path.GetFullPath(urlfilePath);

            //動画の公開 URL 一覧を取得する
            List<String> fileSasUrlList = new List<String>();
            foreach (IAssetFile file in asset.AssetFiles)
            {
                string sasUrl = BuildFileSasUrl(file, locator);
                fileSasUrlList.Add(sasUrl);
                WriteToFile(outFilePath, sasUrl);
            }
        }
        #endregion

        #region サムネイル作成・公開
        //動画からサムネイルを作成
        private static void CreateThumbnails(CloudMediaContext context, string assetName)
        {
            //MediaService 制御用のコンテキスト作成
            var asset = context.Assets.Where(_ => _.Name == assetName).ToList()[0];

            {
                var job = context.Jobs.Create("サムネイル Encoding Job");
                var processor = GetMediaProcessor("Windows Azure Media Encoder", context);
                var task = job.Tasks.AddNew("Thumbnails Encoding Task",
                    processor,
                    "Thumbnails",
                    TaskOptions.None);
                task.InputAssets.Add(asset);
                task.OutputAssets.AddNew(assetName + " - サムネイルズ", AssetCreationOptions.None);

                //このメソッドは同期だが、job の実行完了はまたない
                job.Submit();
            }
        }

        //作成したサムネイルを公開
        private static void PublishThumbnails(CloudMediaContext context, string assetName, string urlfilePath)
        {
            var asset = context.Assets.Where(_ => _.Name == assetName + @" - サムネイルズ").ToList()[0];

            //Locator は 1アセットに10個までなので、古い Locator は削除する
            Console.WriteLine("古いロケーターを削除");
            foreach (var oldlocator in asset.Locators)
            {
                oldlocator.Delete();
            }

            //Locator に割り当てる公開用の情報を設定
            IAccessPolicy accessPolicy =
                context.AccessPolicies.Create("30日読みとり許可", TimeSpan.FromDays(30), AccessPermissions.Read);
            ILocator locator =
                context.Locators.CreateLocator(LocatorType.Sas, asset, accessPolicy, DateTime.UtcNow.AddDays(-1));

            //動画ファイルのURL格納先
            string outFilePath = Path.GetFullPath(urlfilePath);

            //動画の公開 URL 一覧を取得する
            List<String> fileSasUrlList = new List<String>();
            foreach (IAssetFile file in asset.AssetFiles)
            {
                string sasUrl = BuildFileSasUrl(file, locator);
                fileSasUrlList.Add(sasUrl);
                WriteToFile(outFilePath, sasUrl);
            }
        }
        #endregion

        #region ユーティリティ系メソッド
        //エンコーディングのプロセッサを取得する
        private static IMediaProcessor GetMediaProcessor(string mediaProcessor, CloudMediaContext context)
        {
            // Query for a media processor to get a reference.
            var theProcessor =
                                from p in context.MediaProcessors
                                where p.Name == mediaProcessor
                                select p;
            // Cast the reference to an IMediaprocessor.
            IMediaProcessor processor = theProcessor.First();

            if (processor == null)
            {
                throw new ArgumentException(string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    "Unknown processor",
                    mediaProcessor));
            }
            return processor;
        }

        //Locator の割り当てたURLを取得する
        static string BuildFileSasUrl(IAssetFile file, ILocator locator)
        {
            // locatorのパスを得るためには、SAS URL にファイル名を結合する
            var uriBuilder = new UriBuilder(locator.Path);
            uriBuilder.Path = uriBuilder.Path + "/" + file.Name;

            //SAS URL を返す
            return uriBuilder.Uri.AbsoluteUri;
        }

        //URL情報をはきだす
        static void WriteToFile(string outFilePath, string fileContent)
        {
            StreamWriter sr = File.AppendText(outFilePath);
            sr.WriteLine(fileContent);
            sr.Close();
        }
        #endregion
    }
}
using System;
using System.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Threading;
using System.Threading.Tasks;

namespace MediaConsoleApp
{
    class Program
    {
        //Media Service のアカウント名とキー名 - App.config を編集してください
        private static string _accountName = ConfigurationManager.AppSettings["AccountName"];
        private static string _accountKey = ConfigurationManager.AppSettings["AccountKey"];

        //適宜変更して欲しいパス名 - App.config を編集してください
        private static string _moviefilePath = ConfigurationManager.AppSettings["MovieFilePath"];
        private static string _urlfilePath = ConfigurationManager.AppSettings["UrlFilePath"];

        static void Main(string[] args)
        {
            //管理ポータルに表示される
            string assetName = "新規動画アセット";

            Console.WriteLine("------------ アプリケーション開始 ------------");

            CloudMediaContext context = new CloudMediaContext(_accountName, _accountKey);

            //① 動画のアップロード（※注 エンコード完了には時間がかかります）
            Console.WriteLine("①動画のアップロード～公開迄を実施");
            UploadSimpleAsset(context, assetName, _moviefilePath);
            EncodeSimpleAsset(context, assetName);
            PublishSimpleAsset(context, assetName, _urlfilePath);

            Console.WriteLine("");

            //② 既存のアセットに対するサムネイルの作成＆公開（※注 エンコード完了には時間がかかります）
            Console.WriteLine("②アップロードした動画からサムネイルを生成と公開を実施");
            EncodeToThumbnails(context, assetName);
            PublishThumbnails(context, assetName, _urlfilePath);

            Console.WriteLine("------------ アプリケーション終了   ------------");
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

            Console.WriteLine("アップロード開始");
            //アップロードのメソッドは非同期版(UploadAsync)も存在
            assetFile.Upload(moviefilePath);
            Console.WriteLine("アップロード終了");
        }

        static void AssetFile_UploadProgressChanged(object sender, UploadProgressChangedEventArgs e)
        {
            //現在のファイルアップロード状況を確認
            Console.WriteLine("★{1}/{2} bytes の {0}% アップロード ", e.Progress, e.BytesUploaded, e.TotalBytes);
        }

        private static void EncodeSimpleAsset(CloudMediaContext context, string assetName)
        {
            var asset = context.Assets.Where(_ => _.Name == assetName).FirstOrDefault();

            //ジョブの作成
            Console.WriteLine("ジョブの作成を開始");
            var job = context.Jobs.Create("動画 Encoding Job");
            var task = job.Tasks.AddNew("動画 Encoding Task",
                GetMediaProcessor("Windows Azure Media Encoder", context),
                "VC1 Smooth Streaming 720p",
                // サンプルは Smooth Streaming の動画をエンコード
                // ここの引数を以下のMSDNを参考に変更することで、エンコードを変更可能
                // http://msdn.microsoft.com/en-us/library/windowsazure/jj129582.aspx
                TaskOptions.None);
            task.InputAssets.Add(asset);
            task.OutputAssets.AddNew(assetName + " - VC1 Smooth Streaming 720p", AssetCreationOptions.None);

            //ジョブの実行
            Console.WriteLine("ジョブの実行");
            job.Submit();

            //ジョブの処理中は待つ
            bool isJobComplete = false;
            while (isJobComplete == false)
            {
                switch (job.State)
                {
                    case JobState.Scheduled:
                    case JobState.Queued:
                    case JobState.Processing:
                        job = context.Jobs.Where(_ => _.Id == job.Id).FirstOrDefault();
                        Console.WriteLine("★ジョブ：{0} at {1} を処理中...", job.Name, job.State);
                        Thread.Sleep(10000);
                        break;
                    case JobState.Finished:
                        Console.WriteLine("★ジョブ：{0} の処理が完了", job.Name, job.State);
                        isJobComplete = true;
                        break;
                }
            }
        }

        private static void PublishSimpleAsset(CloudMediaContext context, string assetName, string urlfilePath)
        {
            // 表示用の動画ファイルのURL格納先
            string outFilePath = Path.GetFullPath(urlfilePath);

            // assetName で始まるアセットを取得
            var assets = context.Assets.Where(_ => _.Name.StartsWith(assetName));

            //一つのアセットに割り当てられるlocatorは10個までなので、古いlocator情報を削除
            Console.WriteLine("古いlocatorを削除");
            foreach (var locator in assets.ToList().SelectMany(_ => _.Locators))
            {
                locator.Delete();
            }

            //Locator の割り当て
            Console.WriteLine("公開用Locatorの割り当て");
            IAccessPolicy accessPolicy =
                context.AccessPolicies.Create("30日読みとり許可", TimeSpan.FromDays(30), AccessPermissions.Read);

            //locatorを割り当て、URLをファイルに出力する
            foreach (var asset in assets)
            {
                List<String> fileSasUrlList = new List<String>();
                foreach (IAssetFile file in asset.AssetFiles)
                {
                    ILocator locator = null;
                    if (file.Name.ToLower().EndsWith(".ism"))
                    {
                        locator = context.Locators.CreateLocator(LocatorType.OnDemandOrigin, asset, accessPolicy, DateTime.UtcNow.AddDays(-1));
                    }
                    else if (file.Name.ToLower().EndsWith(".jpg") || file.Name.ToLower().EndsWith(".mp4"))
                    {
                        locator = context.Locators.CreateLocator(LocatorType.Sas, asset, accessPolicy, DateTime.UtcNow.AddDays(-1));
                    }
                    else
                    {
                        continue;
                    }

                    string sasUrl = BuildFileSasUrl(file, locator);
                    fileSasUrlList.Add(sasUrl);
                    WriteToFile(outFilePath, sasUrl);
                }
            }
        }
        #endregion

        #region サムネイル作成・公開
        //動画からサムネイルを作成
        private static void EncodeToThumbnails(CloudMediaContext context, string assetName)
        {
            //MediaService 制御用のコンテキスト作成
            var asset = context.Assets.Where(_ => _.Name == assetName).FirstOrDefault();

            var job = context.Jobs.Create("サムネイル Encoding Job");
            var processor = GetMediaProcessor("Windows Azure Media Encoder", context);
            var task = job.Tasks.AddNew("Thumbnails Encoding Task",
                processor,
                "Thumbnails",
                TaskOptions.None);
            task.InputAssets.Add(asset);
            task.OutputAssets.AddNew(assetName + " - サムネイルズ", AssetCreationOptions.None);

            //このメソッドは同期だが、ジョブ自体の実行完了はまたない
            Console.WriteLine("サムネイルのジョブを実行");
            job.Submit();

            //ジョブの処理中は待つ
            bool isJobComplete = false;
            while (isJobComplete == false)
            {
                switch (job.State)
                {
                    case JobState.Scheduled:
                    case JobState.Queued:
                    case JobState.Processing:
                        job = context.Jobs.Where(_ => _.Id == job.Id).FirstOrDefault();
                        Console.WriteLine("★ジョブ：{0} at {1} を処理中...", job.Name, job.State);
                        Thread.Sleep(10000);
                        break;
                    case JobState.Finished:
                        Console.WriteLine("★ジョブ：{0} の処理が完了", job.Name, job.State);
                        isJobComplete = true;
                        break;
                }
            }
        }

        //作成したサムネイルを公開
        private static void PublishThumbnails(CloudMediaContext context, string assetName, string urlfilePath)
        {
            var asset = context.Assets.Where(_ => _.Name == assetName + @" - サムネイルズ").FirstOrDefault();

            //Locator は 1アセットに10個までなので、古い Locator は削除する
            Console.WriteLine("古いロケーターを削除");
            foreach (var oldlocator in asset.Locators)
            {
                oldlocator.Delete();
            }

            //Locator に割り当てる公開用の情報を設定
            Console.WriteLine("サムネイルの公開");
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
            Console.WriteLine("{0} ファイルに公開情報を格納", outFilePath);
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
            if (locator.Type == LocatorType.OnDemandOrigin)
            {
                return new Uri(locator.Path + file.Name + "/Manifest").ToString();
            }
            else if (locator.Type == LocatorType.None || locator.Type == LocatorType.Sas)
            {
                var uriBuilder = new UriBuilder(locator.Path);
                uriBuilder.Path = uriBuilder.Path + "/" + file.Name;
                //SAS URL を返す
                return uriBuilder.Uri.AbsoluteUri;
            }
            return string.Empty;
        }

        //URL情報をはきだす
        static void WriteToFile(string outFilePath, string fileContent)
        {
            if (string.IsNullOrWhiteSpace(fileContent))
            {
                return;
            }
            StreamWriter sr = File.AppendText(outFilePath);
            sr.WriteLine(fileContent);
            sr.Close();
        }
        #endregion
    }
}

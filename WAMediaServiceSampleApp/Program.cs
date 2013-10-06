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
        // メディアサービスのアカウント名とキー名 - App.config を編集してください
        private static string _accountName = ConfigurationManager.AppSettings["AccountName"];
        private static string _accountKey = ConfigurationManager.AppSettings["AccountKey"];

        // 動画ファイルパス、動画公開URLを記載するファイルパス - App.config を編集してください
        private static string _moviefilePath = ConfigurationManager.AppSettings["MovieFilePath"];
        private static string _urlfilePath = ConfigurationManager.AppSettings["UrlFilePath"];

        static void Main(string[] args)
        {
            //管理ポータルに表示されるアセット名を指定します
            string assetName = "新規動画アセット";

            Console.WriteLine("------------ アプリケーション開始 ------------");

            CloudMediaContext context = new CloudMediaContext(_accountName, _accountKey);

            //① 動画をアップロードし、エンコードした後に公開します（※注 エンコード完了には時間がかかります）
            Console.WriteLine("①動画のアップロード～公開迄を実施");
            UploadSimpleAsset(context, assetName, _moviefilePath);
            EncodeSimpleAsset(context, assetName);
            PublishSimpleAsset(context, assetName, _urlfilePath);

            Console.WriteLine("");

            //② 動画に対するサムネイルを作成し、公開します（※注 エンコード完了には時間がかかります）
            // Console.WriteLine("②アップロードした動画からサムネイルを生成と公開を実施");
            // EncodeToThumbnails(context, assetName);
            // PublishThumbnails(context, assetName, _urlfilePath);

            Console.WriteLine("------------ アプリケーション終了   ------------");
            Console.ReadLine();
        }

        #region 動画アップロード → 公開までのサンプルコード
        private static void UploadSimpleAsset(CloudMediaContext context, string assetName, string moviefilePath)
        {
            // アセットのインスタンスを作成時、ストレージ暗号化はしません
            var asset = context.Assets.Create(assetName,
                AssetCreationOptions.None);

            // 作成したアセットに格納するアセット・ファイルをファイル名から作成します
            var assetFile = asset.AssetFiles.Create(Path.GetFileName(moviefilePath));

            // アップロード進捗（しんちょく）を確認するためのハンドラを追加します
            assetFile.UploadProgressChanged += (sender, e) =>
                Console.WriteLine("★ {0}% uploaded. {1}/{2} bytes",
                e.Progress,
                e.BytesUploaded,
                e.TotalBytes);

            Console.WriteLine("アップロード開始");
            assetFile.Upload(moviefilePath);
            Console.WriteLine("アップロード終了");
        }

        static void AssetFile_UploadProgressChanged(object sender, UploadProgressChangedEventArgs e)
        {
            // 現在のファイル・アップロード状況を出力します
            Console.WriteLine("★{1}/{2} bytes の {0}% アップロード ", e.Progress, e.BytesUploaded, e.TotalBytes);
        }

        private static void EncodeSimpleAsset(CloudMediaContext context, string assetName)
        {
            var asset = context.Assets.Where(_ => _.Name == assetName).FirstOrDefault();

            // ジョブの作成
            Console.WriteLine("ジョブの作成を開始");
            var job = context.Jobs.Create("動画 Encoding Job");
            var task = job.Tasks.AddNew("動画 Encoding Task",
                GetMediaProcessor("Windows Azure Media Encoder", context),
                "VC1 Smooth Streaming 720p",
                // サンプルは Smooth Streaming の動画をエンコードする
                // 引数を以下のMSDNを参考に変更することで、ほかの形式の動画ファイルにエンコードを変更可能
                // http://msdn.microsoft.com/en-us/library/windowsazure/jj129582.aspx
                TaskOptions.None);
            task.InputAssets.Add(asset);
            task.OutputAssets.AddNew(assetName + " - VC1 Smooth Streaming 720p", AssetCreationOptions.None);

            // ジョブを実行します
            Console.WriteLine("ジョブの実行");
            job.Submit();

            // ジョブの処理中のステータスを出力します。
            bool isJobComplete = false;
            while (isJobComplete == false)
            {
                switch (job.State)
                {
                    case JobState.Scheduled:
                    case JobState.Queued:
                    case JobState.Processing:
                        job = context.Jobs.Where(_ => _.Id == job.Id).FirstOrDefault();
                        Console.WriteLine("★ジョブ名={0}, 状態={1}", job.Name, job.State);
                        Thread.Sleep(10000);
                        break;
                    case JobState.Finished:
                        Console.WriteLine("★ジョブ名={0}, 状態={1}", job.Name, job.State);
                        isJobComplete = true;
                        break;
                }
            }
            // ジョブの実行待ちは以下のコードでも可能だが、ジョブの状態は確認できないため用途に応じて利用して下さい
            // job.GetExecutionProgressTask(CancellationToken.None).Wait();
        }

        private static void PublishSimpleAsset(CloudMediaContext context, string assetName, string urlfilePath)
        {
            // 動画ファイルの公開URLを記載するファイルを指定します
            string outFilePath = Path.GetFullPath(urlfilePath);

            // assetName で始まるアセットを取得します
            var assets = context.Assets.Where(_ => _.Name.StartsWith(assetName));

            // 1つのアセットに割り当てられるロケータは10個までなので、古いロケーター情報を削除します
            Console.WriteLine("古いlocatorを削除");
            foreach (var locator in assets.ToList().SelectMany(_ => _.Locators))
            {
                locator.Delete();
            }

            // 公開用ロケーターを割り当てます
            Console.WriteLine("公開用Locatorの割り当て");
            IAccessPolicy accessPolicy =
                context.AccessPolicies.Create("30日読みとり許可", TimeSpan.FromDays(30), AccessPermissions.Read);

            // 公開用ロケーターを動画に割り当て、公開した動画のURLをファイルに出力します
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
                        Console.WriteLine("★ジョブ名={0}, 状態={1}", job.Name, job.State);
                        Thread.Sleep(10000);
                        break;
                    case JobState.Finished:
                        Console.WriteLine("★ジョブ名={0}, 状態={1}", job.Name, job.State);
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

        //ロケータの割り当てたURLを取得する
        static string BuildFileSasUrl(IAssetFile file, ILocator locator)
        {
            // ロケータのパスを得るため、SAS URL にファイル名を結合する
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

        //URL情報をファイルに出力する
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

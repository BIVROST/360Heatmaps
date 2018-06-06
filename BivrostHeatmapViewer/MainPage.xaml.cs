using Microsoft.Graphics.Canvas.Effects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using System.Drawing;


//camera capture
using Windows.Media.Capture;
using Windows.ApplicationModel;
using System.Threading.Tasks;
using Windows.System.Display;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Threading;
using Windows.Media.Editing;
using Windows.Graphics.Imaging;
using Windows.Media.Playback;
using System.Collections.ObjectModel;
using Windows.Media.Transcoding;



// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BivrostHeatmapViewer
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	/// 
	public delegate void saveProgressCallback(string message);
	public sealed partial class MainPage : Page
	{
		StorageFile videoFile;
		WriteableBitmap wb;
		private MediaComposition composition;
		private MediaStreamSource mediaStreamSource;
		List<MediaStreamSource> heatmaps;
		private MediaPlayer mediaPlayer;
		private Rect rect = new Rect(0, 0, 1280, 720);
		

		private ObservableCollection<Session> _items = new ObservableCollection<Session>();

		public ObservableCollection<Session> Items
		{
			get { return this._items; }
		}



		/*
		public async void Preview(byte[] pixels, int width, int height)
		{
			await heatImage.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () => {
				if (wb == null || wb.PixelWidth != width || wb.PixelHeight != height)
				{
					wb = new WriteableBitmap(width, height);
					heatImage.Source = wb;
				}

				using (Stream stream = wb.PixelBuffer.AsStream())
				{
					await stream.WriteAsync(pixels, 0, pixels.Length);
				}
				//composition.Clips.Add();
				//heatImage.Source = wb;
			});
		}
		*/
		public MainPage()
		{
			this.InitializeComponent();

			
			//InitializeFrostedGlass(mediaPlayerElement);
			InitializeDropShadow(mainPanel, previewImage);
			//ListOfSessions = new ListOfSessions();
			//pickFolderButton.Click += pickFolderButton_Click;
			//rect = Rect((int)mediaPlayerElement.ActualWidth, (int)mediaPlayerElement.ActualHeight);

			heatmapSessionsListView.SelectionChanged += (s, e) =>
			{
				previewButton.IsEnabled = heatmapSessionsListView.SelectedItems.Count > 0;
			};
			previewButton.IsEnabled = heatmapSessionsListView.SelectedItems.Count > 0;

			//HeatmapList.Add();




			//previewButton.Click += PreviewButton_Click;

			//debugButton.Click += async (s, e) =>
			//{
			//	StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Sessions/sessions-okrutna.js"));
			//	var json = await Windows.Storage.FileIO.ReadTextAsync(file);
			//	var sessionCollection = JsonConvert.DeserializeObject<SessionCollection>(json);
			//	;

			//	RenderTargetBitmap rtb = new RenderTargetBitmap();
			//	await rtb.RenderAsync(mainGridControl);
			//	heatImage.Source = rtb;
			//	//Windows.Media.Editing.MediaClip.CreateFromSurface(rtb., TimeSpan.FromSeconds(1));

			//};
		}

		/*private async void GenerateVideoFromHeatmap()
		{
			List<byte[]> heatmaps = new List<byte[]>();
			wb = new WriteableBitmap(64, 64);
			composition = new MediaComposition();

			int count = heatmapSessionsListView.Items.Count;

			for (int i = 1; i < count - 1; i++)
			{
				StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Sessions/session0" + i + ".bvr"));
				var json = await Windows.Storage.FileIO.ReadTextAsync(file);
				var session = JsonConvert.DeserializeObject<Session>(json);

				var deserializedData = Heatmap.CoordsDeserialize(session.history);
				var heatmap = Heatmap.Generate(deserializedData);
				byte[] pixels = Heatmap.RenderHeatmap(heatmap);
				heatmaps.Add(pixels);
			}

			var clip = await MediaClip.CreateFromFileAsync(videoFile);
			
			MediaOverlay videoOverlay = new MediaOverlay(clip);
			videoOverlay.Position = rect;
			videoOverlay.Opacity = 0.7;
			videoOverlay.AudioEnabled = true;

			var bR = videoBackgroundPicker.Color.R;
			var bG = videoBackgroundPicker.Color.G;
			var bB = videoBackgroundPicker.Color.B;
			Windows.UI.Color videoBackgoundColor = Windows.UI.Color.FromArgb(255,bR, bG, bB);

			var videoBackground = MediaClip.CreateFromColor(videoBackgoundColor, new TimeSpan(0, 0, 20));

			composition.Clips.Add(videoBackground);
			
			
			//composition.Clips.Add(await MediaClip.CreateFromImageFileAsync(await WriteableBitmapToStorageFile(wb, FileFormat.Png), new TimeSpan(0, 0, 3)));

			mediaStreamSource = composition.GeneratePreviewMediaStreamSource(
				(int)mediaPlayerElement.ActualWidth,
				(int)mediaPlayerElement.ActualHeight);


			MediaOverlay mediaOverlay; 


			MediaOverlayLayer mediaOverlayLayer = new MediaOverlayLayer();
			mediaOverlayLayer.Overlays.Add(videoOverlay);

			TimePicker timePicker = new TimePicker();

			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			videoLoading.Visibility = Visibility.Visible;

			for (int i = 1; i < 200; i++)
			{
				using (Stream stream = wb.PixelBuffer.AsStream())
				{
					await stream.WriteAsync(heatmaps[(i%7)+1], 0, heatmaps[(i%7)+1].Length);
					var overlayMediaClip = await MediaClip.CreateFromImageFileAsync(await WriteableBitmapToStorageFile(wb, FileFormat.Tiff), new TimeSpan(0, 0, 0, 0, 100));
					mediaOverlay = new MediaOverlay(overlayMediaClip);
					mediaOverlay.Delay = new TimeSpan(0, 0, 0, 0, (i - 1) * 100);
					mediaOverlay.Position = rect;
					mediaOverlay.Opacity = 0.35;
					mediaOverlayLayer.Overlays.Add(mediaOverlay);
					
				}
				videoLoading.Value = i * 100 / 200;
			}

			videoLoading.Visibility = Visibility.Collapsed;

			stopwatch.Stop();

			composition.OverlayLayers.Add(mediaOverlayLayer);



			mediaPlayerElement.Source = MediaSource.CreateFromMediaStreamSource(mediaStreamSource);

			//deserializedData.Clear();
			//deserializedData.Add(new Heatmap.Coord() { yaw = 63, pitch = 63, fov = 20 });


			//Preview(Heatmap.RenderHeatmap(heatmap), 64, 64);
			//heatImage.Source = wb;

			//for (int i = 0; i < count-1; i++)
			//{
			//Preview(heatmaps[0], 64, 64);
			//Thread.Sleep(2000);

			//Preview(heatmaps[1], 64, 64);
			//Preview(heatmaps[2], 64, 64);
			//Preview(heatmaps[3], 64, 64);
			//Preview(heatmaps[4], 64, 64);
			//}
			debugInfo.Text = "Time: " + ((stopwatch.ElapsedMilliseconds) / 1000).ToString() + "s. ";

		}
		*/

		private void ShowHeatmapGenerating ()
		{
			heatmapLoadingIndicator.Visibility = Visibility.Visible;
			heatmapLoadingIndicator.IsActive = true;
		}

		private void HideHeatmapGenerating ()
		{
			heatmapLoadingIndicator.IsActive = false;
			heatmapLoadingIndicator.Visibility = Visibility.Collapsed;
		}

		private async void ListFolderButton_Click(object sender, RoutedEventArgs e)
		{
			
			
			StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Sessions/session01.bvr"));
			/*
			statici++;
			if (statici > 9)
				statici = 1;
			var json = await Windows.Storage.FileIO.ReadTextAsync(file);
			var session = JsonConvert.DeserializeObject<Session>(json);

	*/

			//przekazac sesje
			//mediaPlayerElement.Visibility = Visibility.Collapsed;
			//StaticHeatmapGenerator.GenerateHeatmap(session, heatImage, 64, 64);

			//var deserializedData = Heatmap.CoordsDeserialize("!F75!K2");

			//deserializedData.Clear();
			//deserializedData.Add(new Heatmap.Coord() { yaw = 32, pitch = 32, fov = 45 });

			//var heatmap = Heatmap.Generate(deserializedData);
			//Preview(Heatmap.RenderHeatmap(heatmap), 64, 64);


			//Stopwatch w1 = new Stopwatch();
			//w1.Start();

			
			//for (int it = 0; it < 1000; it++)
			//{
			//	deserializedData = Heatmap.CoordsDeserialize(session.history);
			//	heatmap = Heatmap.Generate(deserializedData);
			//	var imageMap = Heatmap.RenderHeatmap(heatmap);
			//}
			//w1.Stop();

			//System.Diagnostics.Debug.WriteLine(1000f / (w1.ElapsedMilliseconds / 1000f));
			//fpsLabel.Text = (1000f / (w1.ElapsedMilliseconds / 1000f)).ToString();
			//;

			//FileOpenPicker filePicker = new FileOpenPicker();
			//filePicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
			//filePicker.FileTypeFilter.Add(".txt");
			//filePicker.FileTypeFilter.Add(".bvr");
			//filePicker.FileTypeFilter.Add(".json");
			//StorageFile file = await filePicker.PickSingleFileAsync();

			//if (file != null)
			//{
			//	StorageApplicationPermissions.FutureAccessList.AddOrReplace("heatmap-test", file);

			//	var json = await Windows.Storage.FileIO.ReadTextAsync(file);
			//	var session = JsonConvert.DeserializeObject<Session>(json);
			//	;

			//}
			//else
			//{
			//	;
			//}


			//StorageFolder folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync("PickedFolderToken");
			//var items = await folder.GetItemsAsync();
			//foreach(var i in items)
			//{
			//	System.Diagnostics.Debug.WriteLine(i.Name + " " + (uint)i.Attributes);
			//	if(i.Attributes == Windows.Storage.FileAttributes.Directory)
			//	{
			//		var f = i as StorageFolder;


			//		foreach(var x in await f.GetItemsAsync())
			//		{
			//			System.Diagnostics.Debug.WriteLine("\t\t" + x.Name + " " + x.Attributes);
			//		}
			//	}
			//}
			//;
		}

		private void InitializeFrostedGlass(UIElement glassHost)
		{
			Visual hostVisual = ElementCompositionPreview.GetElementVisual(glassHost);
			Compositor compositor = hostVisual.Compositor;

			

			// Create a glass effect, requires Win2D NuGet package
			var glassEffect = new GaussianBlurEffect
			{
				BlurAmount = 50.0f,
				BorderMode = EffectBorderMode.Hard,
				Source = new CompositionEffectSourceParameter("backdropBrush")
			};

			//  Create an instance of the effect and set its source to a CompositionBackdropBrush
			var effectFactory = compositor.CreateEffectFactory(glassEffect);
			var backdropBrush = compositor.CreateBackdropBrush();
			var effectBrush = effectFactory.CreateBrush();

			effectBrush.SetSourceParameter("backdropBrush", backdropBrush);

			// Create a Visual to contain the frosted glass effect
			var glassVisual = compositor.CreateSpriteVisual();
			glassVisual.Brush = effectBrush;

			// Add the blur as a child of the host in the visual tree
			ElementCompositionPreview.SetElementChildVisual(glassHost, glassVisual);

			// Make sure size of glass host and glass visual always stay in sync
			var bindSizeAnimation = compositor.CreateExpressionAnimation("hostVisual.Size");
			bindSizeAnimation.SetReferenceParameter("hostVisual", hostVisual);

			glassVisual.StartAnimation("Size", bindSizeAnimation);
		}

		private void InitializeDropShadow(UIElement shadowHost, Shape shadowTarget)
		{
			Visual hostVisual = ElementCompositionPreview.GetElementVisual(shadowHost);
			Compositor compositor = hostVisual.Compositor;

			// Create a drop shadow
			var dropShadow = compositor.CreateDropShadow();
			dropShadow.Color = Windows.UI.Color.FromArgb(255, 75, 75, 80);
			dropShadow.BlurRadius = 25.0f;
			dropShadow.Offset = new Vector3(2.5f, 2.5f, 0.0f);
			// Associate the shape of the shadow with the shape of the target element
			dropShadow.Mask = shadowTarget.GetAlphaMask();

			// Create a Visual to hold the shadow
			var shadowVisual = compositor.CreateSpriteVisual();
			shadowVisual.Shadow = dropShadow;

			// Add the shadow as a child of the host in the visual tree
			ElementCompositionPreview.SetElementChildVisual(shadowHost, shadowVisual);

			// Make sure size of shadow host and shadow visual always stay in sync
			var bindSizeAnimation = compositor.CreateExpressionAnimation("hostVisual.Size");
			bindSizeAnimation.SetReferenceParameter("hostVisual", hostVisual);

			shadowVisual.StartAnimation("Size", bindSizeAnimation);
		}

		private async void pickFolderButton_Click(object sender, RoutedEventArgs e)
		{
			FolderPicker folderPicker = new FolderPicker();
			folderPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
			folderPicker.FileTypeFilter.Add(".txt");
			StorageFolder folder = await folderPicker.PickSingleFolderAsync();
			
			if(folder != null)
			{
				StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);
			} else
			{
				;
			}
		}

		private async void Rectangle_Drop(object sender, DragEventArgs e)
		{
			if (e.DataView.Contains(StandardDataFormats.StorageItems))
			{
				var items = await e.DataView.GetStorageItemsAsync();
				if (items.Count > 0)
				{
					var storageFile = items[0] as StorageFile;

					IRandomAccessStream f = await storageFile.OpenAsync(FileAccessMode.Read);
				}
			}
		}

		private void Rectangle_DragOver(object sender, DragEventArgs e)
		{
			e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
		}

		private void SelectedVideo_DragOver(object sender, DragEventArgs e)
		{
			e.AcceptedOperation = DataPackageOperation.Copy;
		}

		private async void SelectedVideo_Drop(object sender, DragEventArgs e)
		{
			if (e.DataView.Contains(StandardDataFormats.StorageItems))
			{
				var items = await e.DataView.GetStorageItemsAsync();
				if (items.Count > 0)
				{
					var file = items[0] as StorageFile;
					if (file.ContentType == "video/mp4" || file.ContentType == "video/x-matroska")
					{
						videoFile = file;
						selectedVideoFileTextBlock.Text = videoFile.DisplayName;
						//mediaPlayerElement.Source = MediaSource.CreateFromStorageFile(videoFile);
					}
				}
			}
		}
/*
		private void playButton_Click(object sender, RoutedEventArgs e)
		{
			mediaStreamSource = composition.GenerateMediaStreamSource();

			if (mediaPlayer == null)
				mediaPlayer = new MediaPlayer();

			//mediaPlayer.CommandManager.IsEnabled = true;
			mediaPlayerElement.Source = MediaSource.CreateFromMediaStreamSource(mediaStreamSource);
			mediaPlayer = mediaPlayerElement.MediaPlayer;
			mediaPlayer.Play();		
			
		}
		*/
		private void selectAllHeatmaps_Click(object sender, RoutedEventArgs e)
		{
			heatmapSessionsListView.SelectionMode = ListViewSelectionMode.Multiple;
			heatmapSessionsListView.SelectAll();
		}

		private void deselectAllHeatmaps_Click(object sender, RoutedEventArgs e)
		{
			heatmapSessionsListView.SelectionMode = ListViewSelectionMode.Multiple;
			heatmapSessionsListView.SelectedIndex = -1;
		}

		private void selectionModeHeatmaps_Click(object sender, RoutedEventArgs e)
		{
			if (heatmapSessionsListView.SelectionMode == ListViewSelectionMode.Multiple)
			{
				heatmapSessionsListView.SelectionMode = ListViewSelectionMode.Single;
				selectAllHeatmaps.Visibility = Visibility.Collapsed;
				deselectAllHeatmaps.Visibility = Visibility.Collapsed;
			}
			else
			{
				heatmapSessionsListView.SelectionMode = ListViewSelectionMode.Multiple;
				selectAllHeatmaps.Visibility = Visibility.Visible;
				deselectAllHeatmaps.Visibility = Visibility.Visible;
			}
		}

		private void deleteSelectedHeatmaps_Click(object sender, RoutedEventArgs e)
		{
			if(heatmapSessionsListView.SelectedItems.Count > 0)
			{
				var listItems = heatmapSessionsListView.SelectedItems.ToList();
				foreach (Session i in listItems)
				{
					Items.Remove(i);
				}
			}
		}

		private async Task<StorageFile> loadFile (params string[] fileTypes)
		{
			FileOpenPicker openPicker = new FileOpenPicker();
			StorageFile file;

			openPicker.ViewMode = PickerViewMode.Thumbnail;
			openPicker.SuggestedStartLocation = PickerLocationId.Desktop;

			foreach (String s in fileTypes)
			{
				openPicker.FileTypeFilter.Add(s);
			}

			file = await openPicker.PickSingleFileAsync();

			return file;

		}

		private async void addHeatmaps_Click(object sender, RoutedEventArgs e)
		{
			string json;
			SessionCollection sessionCollection;
			StorageFile file;

			ShowHeatmapLoading();

			file = await loadFile(".bvr", ".js", ".txt");
			
			
			if (file != null)
			{
				json = await Windows.Storage.FileIO.ReadTextAsync(file);
				sessionCollection = JsonConvert.DeserializeObject<SessionCollection>(json);
				
				foreach (Session s in sessionCollection.sessions)
				{
					Items.Add(s);
				}
				
			}
			HideHeatmapLoading();

			/*
			var result = await StaticHeatmapGenerator.GenerateHeatmap(sessionCollection, rect, videoBackgroundPicker, heatmapOpacity.Value/100);
			
			if (mediaPlayer == null)
				mediaPlayer = new MediaPlayer();

			//mediaPlayer.CommandManager.IsEnabled = true;
			mediaPlayerElement.Source = MediaSource.CreateFromMediaStreamSource(result);
			mediaPlayer = mediaPlayerElement.MediaPlayer;

	*/
			
		}

		private void ShowHeatmapLoading()
		{
			heatmapListLoadingScreenGrid.Visibility = Visibility.Visible;
			heatmapListLoadingIndicator.IsActive = true;
		}

		private void HideHeatmapLoading()
		{
			heatmapListLoadingIndicator.IsActive = false;
			heatmapListLoadingScreenGrid.Visibility = Visibility.Collapsed;
		}

		private async void heatmapSessionsListView_Drop(object sender, DragEventArgs e)
		{
			ShowHeatmapLoading();

			if (e.DataView.Contains(StandardDataFormats.StorageItems))
			{
				var items = await e.DataView.GetStorageItemsAsync();
				if (items.Count > 0)
				{
					foreach(var item in items)
					{
						var file = item as StorageFile;
						if (file.FileType == ".bvr" || file.FileType == ".txt" || file.FileType == ".js" || file.FileType == ".json")
						{
							try
							{
								var json = await FileIO.ReadTextAsync(file);
								var session = JsonConvert.DeserializeObject<Session>(json);
								if(session.history != null)
								{
									heatmapSessionsListView.Items.Add(new HeatmapListItem(session));
								}
							}
							catch (Exception exc) {
								;
							};
						}
					}
				}
			}
			await Task.Factory.StartNew(() => System.Threading.Thread.Sleep(2000));
			HideHeatmapLoading();
		}

		private void heatmapSessionsListView_DragOver(object sender, DragEventArgs e)
		{
			e.AcceptedOperation = DataPackageOperation.Link | DataPackageOperation.Copy | DataPackageOperation.Move;
			e.DragUIOverride.Caption = "Load session file";
			//e.DragUIOverride.SetContentFromBitmapImage(null);
			e.DragUIOverride.IsCaptionVisible = true;
			e.DragUIOverride.IsContentVisible = false;
			e.DragUIOverride.IsGlyphVisible = false;
		}

		private void videoBackgroundPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
		{
			colorRect.Fill = new SolidColorBrush(videoBackgroundPicker.Color);
		}

		private void mediaPlayerElement_Loading(FrameworkElement sender, object args)
		{
			//throw new NotImplementedException();
			//TODO: 
		}

		private void CheckBox_Checked(object sender, RoutedEventArgs e)
		{
			horizonImage.Opacity = 0.8;
			horizonImage.Visibility = Visibility.Visible;	
		}

		private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
		{
			horizonImage.Visibility = Visibility.Collapsed;
		}

		private async void GenerateStaticHeatmap(object sender, RoutedEventArgs e)
		{
			mediaPlayerElement.AreTransportControlsEnabled = false;


			ShowHeatmapGenerating();

			SessionCollection sessionCollection = new SessionCollection();
			sessionCollection.sessions = new List<Session>();


			var listItems = heatmapSessionsListView.SelectedItems.ToList();
			foreach (Session s in listItems)
			{
				sessionCollection.sessions.Add(s);
			}

			var result = await StaticHeatmapGenerator.GenerateHeatmap
				(
				sessionCollection,
				rect,
				videoBackgroundPicker,
				heatmapOpacity.Value / 100
				);

			if (mediaPlayer == null)
			{
				mediaPlayer = new MediaPlayer();
				mediaPlayer = mediaPlayerElement.MediaPlayer;
			}


			mediaPlayerElement.Source = MediaSource.CreateFromMediaStreamSource(result);

			//mediaPlayerElement.Visibility = Visibility.Visible;

			HideHeatmapGenerating();
		}

		private async void VideoGenTest(object sender, RoutedEventArgs e)
		{
			composition = new MediaComposition();
			mediaPlayerElement.Source = null;
			SessionCollection sessionCollection = new SessionCollection();
			sessionCollection.sessions = new List<Session>();


			var listItems = heatmapSessionsListView.SelectedItems.ToList();
			foreach (Session s in listItems)
			{
				sessionCollection.sessions.Add(s);
			}

			var result = await StaticHeatmapGenerator.GenerateVideoFromHeatmap
				(
				sessionCollection,
				rect,
				videoBackgroundPicker,
				videoLoading,
				heatmapOpacity.Value/100
				);

			ShowHeatmapGenerating();
			
			if (mediaPlayer == null)
            {
                mediaPlayer = new MediaPlayer();
			}

			
			var video = await MediaClip.CreateFromFileAsync(videoFile);
			MediaOverlayLayer videoOverlayLayer = new MediaOverlayLayer();
			MediaOverlay videoOverlay = new MediaOverlay(video);
			videoOverlay.Opacity = videoOpacity.Value / 100;
			videoOverlay.Position = rect;
			videoOverlay.AudioEnabled = true;

			videoOverlayLayer.Overlays.Add(videoOverlay);
			
			composition.Clips.Add(MediaClip.CreateFromColor(videoBackgroundPicker.Color, video.OriginalDuration));
			composition.OverlayLayers.Add(videoOverlayLayer);
			composition.OverlayLayers.Add(result);

			MediaStreamSource res;
			try
			{

				res = composition.GeneratePreviewMediaStreamSource(1280, 720);
				var md = MediaSource.CreateFromMediaStreamSource(res);
				mediaPlayerElement.Source = md;
			}
			catch (Exception f)
			{
				Debug.WriteLine(f.Message);
			}

			mediaPlayer = mediaPlayerElement.MediaPlayer;
			HideHeatmapGenerating();
			mediaPlayerElement.AreTransportControlsEnabled = true;
			//MediaSource.CreateFromMediaStreamSource

			//Thread.Sleep(2000);


			/*
            for (int i = 0; i < heatmaps.Count - 1; i++)
            {
                mediaPlayerElement.Source = MediaSource.CreateFromMediaStreamSource(heatmaps[i]);
            }
            Console.WriteLine("asd");
            */
		}
		private async void SaveVideo_Click(object sender, RoutedEventArgs e)
		{
			saveProgressCallback saveProgress = ShowErrorMessage;
			await StaticHeatmapGenerator.RenderCompositionToFile(composition, saveProgress);
		}

		private void ShowErrorMessage(string v)
		{
			debugInfo.Text = v;
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			mediaPlayer.Play();
		}
	}
}

using Microsoft.Graphics.Canvas.Effects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

using Windows.UI;
//camera capture
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using Windows.Media.Editing;
using Windows.Media.Playback;
using System.Collections.ObjectModel;
using System.Threading;
using System.Net.Http;
using Windows.UI.Popups;
using Windows.Graphics.Imaging;
using Microsoft.Graphics.Canvas;
using System.Runtime.InteropServices.WindowsRuntime;
using System.IO;
using Microsoft.Graphics.Canvas.UI.Composition;
using System.Runtime.InteropServices;
using Windows.Media.Effects;
using Windows.Foundation.Collections;
using Windows.Media.MediaProperties;
using VideoEffectComponent;


/* TODO: 
 * heatmap list with error markers
 * */


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BivrostHeatmapViewer
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	/// 
	public delegate void saveProgressCallback(double message);
	public sealed partial class MainPage : Page
	{
		public PropertySet valuePairs = new PropertySet();
		StorageFile videoFile;
		StorageFile horizonFile;
		private MediaComposition composition;
		//private MediaComposition mementoComposition;
		private MediaPlayer mediaPlayer;
		private Rect rect = new Rect(0, 0, 4096, 2048);
		MediaClip video;
        public CancellationTokenSource tokenSource = new CancellationTokenSource();
		public CancellationToken token;
		Task<MediaOverlayLayer> task;

		bool dotsFlag = false;
		bool horizonFlag = false;

		public SavingResolutionsCollection resolutions;
		//private static int heatmapListCounter = 0;

		private ObservableCollection<Session> _items = new ObservableCollection<Session>();

		public ObservableCollection<Session> Items
		{
			get { return this._items; }
		}

		public MainPage()
		{
			this.InitializeComponent();
			//InitializeFrostedGlass(mediaPlayerElement);
			InitializeDropShadow(mainPanel, previewImage);

            heatmapSessionsListView.SelectionChanged += (s, e) =>
            {
                previewButton.IsEnabled = heatmapSessionsListView.SelectedItems.Count > 0;
                GenerateButtonEnable();
            };

            saveCompositionButton.IsEnabled = false;
			generateVideoButton.IsEnabled = heatmapSessionsListView.SelectedItems.Count > 0;
			previewButton.IsEnabled = heatmapSessionsListView.SelectedItems.Count > 0;
			
			
			token = tokenSource.Token;
		}

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

		private void InitializeFrostedGlass(UIElement glassHost)
		{
			Visual hostVisual = ElementCompositionPreview.GetElementVisual(glassHost);
			Compositor compositor = hostVisual.Compositor;

			

			// Create a glass effect, requires Win2D NuGet package
			var glassEffect = new GaussianBlurEffect
			{
				BlurAmount = 4f,
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

		private async void addVideoButton_Click(object sender, RoutedEventArgs e)
		{
			StorageFile file;

			file = await loadFile(".mp4");

			if (file != null)
			{
				if (file.ContentType == "video/mp4")
				{
					videoFile = file;
					selectedVideoFileTextBlock.Text = videoFile.DisplayName;
					video = await MediaClip.CreateFromFileAsync(videoFile);
					SetTimeSliders(video.OriginalDuration);
					GenerateButtonEnable();
					var enc = video.GetVideoEncodingProperties();
					resolutions = new SavingResolutionsCollection(enc);
					saveResolutionSelector.ItemsSource = resolutions;
					saveResolutionSelector.SelectedIndex = 0;
				}
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
						video = await MediaClip.CreateFromFileAsync(videoFile);
						SetTimeSliders(video.OriginalDuration);
						GenerateButtonEnable();
						var enc = video.GetVideoEncodingProperties();
						resolutions = new SavingResolutionsCollection(enc);
						saveResolutionSelector.ItemsSource = resolutions;
						saveResolutionSelector.SelectedIndex = 0;
					}
				}
			}
			GenerateButtonEnable();
		}

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
                                var sessionCollection = JsonConvert.DeserializeObject<SessionCollection>(json);
                                foreach (Session s in sessionCollection.sessions)
                                {
                                    Items.Add(s);

								}
                            }
                            catch (Exception exc)
                            {
								Debug.WriteLine(exc.Message);
                            };
						}
					}
				}
			}
			//await Task.Factory.StartNew(() => System.Threading.Thread.Sleep(2000));
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

		private void horizonEnableCheckbox_Checked(object sender, RoutedEventArgs e)
		{
			horizonFlag = true;
		}

		private void horizonEnableCheckbox_Unchecked(object sender, RoutedEventArgs e)
		{
			horizonFlag = false;
		}

		private async void GenerateStaticHeatmap(object sender, RoutedEventArgs e)
		{
			saveCompositionButton.IsEnabled = false;
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
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			horizonEnableCheckbox.IsChecked = false;

			loadingScreen.Visibility = Visibility.Visible;
			buttonLoadingStop.Visibility = Visibility.Visible;

			saveCompositionButton.IsEnabled = false;
			composition = new MediaComposition();
			mediaPlayerElement.Source = null;
			SessionCollection sessionCollection = new SessionCollection();
			sessionCollection.sessions = new List<Session>();


			var listItems = heatmapSessionsListView.SelectedItems.ToList();
			foreach (Session s in listItems)
			{
				sessionCollection.sessions.Add(s);
			}

			bool generateDots;
			if (dotsEnableCheckbox.IsChecked == null)
			{
				generateDots = false;
			}
			else
			{
				generateDots = (bool)dotsEnableCheckbox.IsChecked;
			}

			task = StaticHeatmapGenerator.GenerateVideoFromHeatmap2
				(
				token,
				sessionCollection,
				rect,
				videoBackgroundPicker,
				videoLoading,
				heatmapOpacity.Value / 100,
				videoStartSlider,
				videoStopSlider,
				generateDots
				);

			await task;
			var result = task.Result;

            buttonLoadingStop.Visibility = Visibility.Collapsed;

			ShowHeatmapGenerating();
			
			if (mediaPlayer == null)
            {
                mediaPlayer = new MediaPlayer();
			}

			var video = await MediaClip.CreateFromFileAsync(videoFile);

			MediaOverlayLayer videoOverlayLayer = new MediaOverlayLayer();
			TrimVideo (ref video);
			MediaOverlay videoOverlay = new MediaOverlay(video);
			videoOverlay.Opacity = videoOpacity.Value / 100;
			videoOverlay.Position = rect;
			videoOverlay.AudioEnabled = true;

			videoOverlayLayer.Overlays.Add(videoOverlay);
			
			composition.Clips.Add(MediaClip.CreateFromColor(videoBackgroundPicker.Color, video.TrimmedDuration));
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

            if (token.IsCancellationRequested)
            {
                saveCompositionButton.IsEnabled = false;
            }
            else
            {
                saveCompositionButton.IsEnabled = true;
            }

            tokenSource.Dispose();
            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;

            loadingScreen.Visibility = Visibility.Collapsed;

			stopwatch.Stop();

			debugInfo.Text = stopwatch.Elapsed.TotalSeconds.ToString();
            

		}

		private async void VideoGenTest2(object sender, RoutedEventArgs e)
		{
			//horizonEnableCheckbox.IsChecked = false;
			var ep = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Uhd2160p);
			saveCompositionButton.IsEnabled = false;
			composition = new MediaComposition();
			mediaPlayerElement.Source = null;
			SessionCollection sessionCollection = new SessionCollection();
			sessionCollection.sessions = new List<Session>();


			var listItems = heatmapSessionsListView.SelectedItems.ToList();
			foreach (Session s in listItems)
			{
				sessionCollection.sessions.Add(s);
			}

			StaticHeatmapGenerator.CheckHistoryErrors(sessionCollection);
			int sampleRate;
			StaticHeatmapGenerator.CheckSampleRate(sessionCollection, out sampleRate);

            FillEffectPropertySet(sessionCollection);


            if (mediaPlayer == null)
			{
				mediaPlayer = new MediaPlayer();
			}

			var video = await MediaClip.CreateFromFileAsync(videoFile);

			MediaOverlayLayer videoOverlayLayer = new MediaOverlayLayer();
			TrimVideo(ref video);
			valuePairs.Add("offset", (int)video.TrimTimeFromStart.TotalSeconds);
			var enc = video.GetVideoEncodingProperties();

			valuePairs.Add("frameLength", (1 / ((double)enc.FrameRate.Numerator / enc.FrameRate.Denominator)) * 1000);

			//composition.Clips.Add(MediaClip.CreateFromColor(videoBackgroundPicker.Color, video.TrimmedDuration));
			composition.Clips.Add(video);

			if (horizonFlag)
			{
				composition.OverlayLayers.Add(await generateHorizonLayer((int)video.TrimmedDuration.TotalSeconds, ep.Video.Height, ep.Video.Width));
				//composition.Clips.Add(await generateHorizonLayer((int)video.TrimmedDuration.TotalSeconds, ep.Video.Height, ep.Video.Width));				
			}

			//composition.OverlayLayers.Add(generateOverlayColor((int)video.TrimmedDuration.TotalSeconds, ep.Video.Height, ep.Video.Width));

			var videoEffectDefinition = new VideoEffectDefinition("VideoEffectComponent.HeatmapAddVideoEffect", valuePairs);
			video.VideoEffectDefinitions.Add(videoEffectDefinition);

			MediaStreamSource res;
			try
			{

				valuePairs.Remove("height");
				valuePairs.Remove("width");

				valuePairs.Add("height", ep.Video.Height);
				valuePairs.Add("width", ep.Video.Width);

				res = composition.GenerateMediaStreamSource(ep);
				//res = composition.GeneratePreviewMediaStreamSource(3840, 2160);
				var md = MediaSource.CreateFromMediaStreamSource(res);
				mediaPlayerElement.Source = md;
			}
			catch (Exception f)
			{
				Debug.WriteLine(f.Message);
			}

			mediaPlayer = mediaPlayerElement.MediaPlayer;
			mediaPlayerElement.AreTransportControlsEnabled = true;
			saveCompositionButton.IsEnabled = true;



		}

		private async void SaveVideo_Click(object sender, RoutedEventArgs e)
		{

			tokenSource.Dispose();
			tokenSource = new CancellationTokenSource();
			token = tokenSource.Token;

			MediaEncodingProfile mediaEncoding = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Uhd2160p);

			var enc = video.GetVideoEncodingProperties();

			mediaEncoding.Video.FrameRate.Denominator = enc.FrameRate.Denominator;
			mediaEncoding.Video.FrameRate.Numerator = enc.FrameRate.Numerator;
			mediaEncoding.Video.Bitrate = enc.Bitrate;


			var picker = new Windows.Storage.Pickers.FileSavePicker();
			picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
			picker.FileTypeChoices.Add("MP4 files", new List<string>() { ".mp4" });
			picker.SuggestedFileName = "RenderedVideo.mp4";
			saveProgressCallback saveProgress = ShowErrorMessage;

			Windows.Storage.StorageFile file = await picker.PickSaveFileAsync();
			if (file != null)
			{
				mediaPlayer.Pause();

				var temp = saveResolutionSelector.SelectedItem as Resolutions;

				valuePairs.Remove("height");
				valuePairs.Remove("width");

				valuePairs.Add("height", temp.Resolution.height);
				valuePairs.Add("width", temp.Resolution.width);

				if (dotsFlag)
				{
					valuePairs.Remove("dotsRadius");
					valuePairs.Add("dotsRadius", (float)temp.Resolution.width / 4096 *20);
				}

				//composition.OverlayLayers[0] = generateOverlayColor((int)video.TrimmedDuration.TotalSeconds, temp.Resolution.height, temp.Resolution.width);

				mediaEncoding.Video.Width = temp.Resolution.width;
				mediaEncoding.Video.Height = temp.Resolution.height;
				//valuePairs.Add("save", "1");
				
				buttonLoadingStop.Visibility = Visibility.Visible;
				generateVideoButton.IsEnabled = false;
				saveCompositionButton.IsEnabled = false;

				StaticHeatmapGenerator.RenderCompositionToFile(file, composition, saveProgress, Window.Current, mediaEncoding, token, saveResolutionSelector.SelectedItem);


			}
			//}
		}

		private void ShowErrorMessage(double v)
		{
			if (Equals(v, 100.0))
			{
				videoLoading.Visibility = Visibility.Collapsed;
				loadingScreen.Visibility = Visibility.Collapsed;
				buttonLoadingStop.Visibility = Visibility.Collapsed;
				GenerateButtonEnable();
			}
			else
			{
				videoLoading.Maximum = 100.0;
				loadingScreen.Visibility = Visibility.Visible;
				videoLoading.Visibility = Visibility.Visible;
				videoLoading.Value = v;
			}
		}

		private void GenerateButtonEnable ()
		{
			if (videoFile != null && heatmapSessionsListView.SelectedItems.Count > 0)
			{
				generateVideoButton.IsEnabled = true;
			}
			else
			{
				generateVideoButton.IsEnabled = false;
			}
		}

		private void SetTimeSliders (TimeSpan time)
		{
			videoStartSlider.Maximum = time.TotalSeconds;
			videoStopSlider.Maximum = time.TotalSeconds;
			videoStopSlider.Value = videoStopSlider.Maximum;
		}

		private void TrimVideo (ref MediaClip video)
		{
			int start = (int)videoStartSlider.Value;
			int stop = (int)videoStopSlider.Value;

			if (start > stop)
			{
				videoStopSlider.Value = videoStopSlider.Maximum;
				videoStartSlider.Value = 0;
			}
			else
			{
				video.TrimTimeFromStart = new TimeSpan(0, 0, start);
				video.TrimTimeFromEnd = new TimeSpan(0, 0, (int)(video.OriginalDuration.TotalSeconds - stop));
			}

		}

        private void ButtonLoadingStop_Click(object sender, RoutedEventArgs e)
        {
			buttonLoadingStop.Visibility = Visibility.Collapsed;
			//loadingScreen.Visibility = Visibility.Collapsed;
			ShowErrorMessage(100.0);
			tokenSource.Cancel();
        }

		private void Page_Unloaded(object sender, RoutedEventArgs e)
		{
			if (mediaPlayer != null)
			{
				mediaPlayer.Dispose();
			}
		}

		private void FillEffectPropertySet (SessionCollection sessions)
		{
            valuePairs.Clear();

			var enc = video.GetVideoEncodingProperties();

			var pitch = new List<int>();
			var yaw = new List<int>();
			var fov = new List<int>();

            List<Heatmap.Coord>[] coordsArray = new List<Heatmap.Coord>[sessions.sessions.Count];
            coordsArray[0] = Heatmap.CoordsDeserialize(sessions.sessions[0].history);

            Session session = sessions.sessions[0];
            int min_length = coordsArray[0].Count - 1;

            for (int i = 1; i < sessions.sessions.Count; i++)
            {
                coordsArray[i] = Heatmap.CoordsDeserialize(sessions.sessions[i].history);

                if (min_length > coordsArray[i].Count)
                {
                    min_length = coordsArray[i].Count;
                }
            }

            for (int i = 0; i < min_length; i++)
            {
                for (int k = 0; k < 3; k++)
                {
                    for (int j = 0; j < sessions.sessions.Count; j++)
                    {
                        pitch.Add(coordsArray[j][i].pitch);
                        yaw.Add(coordsArray[j][i].yaw);
                        fov.Add(coordsArray[j][i].fov);
                        //fov.Insert
                    }
                }
            }

			float dotsRadius = 20f;

			valuePairs.Add("backgroundColor", videoBackgroundPicker.Color);
			valuePairs.Add("backgroundOpacity", (float)(1 - videoOpacity.Value / 100));
			valuePairs.Add("dotsRadius", dotsRadius);
            valuePairs.Add("count", sessions.sessions.Count);
			valuePairs.Add("pitch", pitch);
			valuePairs.Add("yaw", yaw);
			valuePairs.Add("fov", fov);
			valuePairs.Add("generateDots", dotsFlag);

			

			valuePairs.Add("height", enc.Height);
			valuePairs.Add("width", enc.Width);
		}

		private void dotsEnableCheckbox_Checked(object sender, RoutedEventArgs e)
		{
			dotsFlag = true;
		}

		private void dotsEnableCheckbox_Unchecked(object sender, RoutedEventArgs e)
		{
			dotsFlag = false;
		}

		private async Task<MediaOverlayLayer> generateHorizonLayer (int timeInSeconds, uint height, uint width)
		{
			

			horizonFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/horizon3840x2160.png"));

			//img.Source = horizonFile;

			MediaOverlayLayer horizonOverlay = new MediaOverlayLayer();


			MediaOverlay mediaOverlay = new MediaOverlay(await MediaClip.CreateFromImageFileAsync(horizonFile, new TimeSpan(0, 0, timeInSeconds))); //generowanie horyzontu
			mediaOverlay.Position = new Rect(0, 0, width, height);
			mediaOverlay.Opacity = 0.9;

	
			horizonOverlay.Overlays.Add(mediaOverlay);

			return horizonOverlay;
		}

		private MediaOverlayLayer generateOverlayColor (int timeInSeconds, uint height, uint width)
		{
			MediaOverlayLayer horizonOverlay = new MediaOverlayLayer();
			

			MediaOverlay mediaOverlay = new MediaOverlay(MediaClip.CreateFromColor(videoBackgroundPicker.Color, new TimeSpan(0, 0, timeInSeconds))); //generowanie horyzontu
			mediaOverlay.Position = new Rect(0, 0, width, height);
			mediaOverlay.Opacity = 1 - videoOpacity.Value / 100;


			horizonOverlay.Overlays.Add(mediaOverlay);

			return horizonOverlay;
		}
	}


}

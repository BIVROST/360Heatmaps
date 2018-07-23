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
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
//camera capture
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Media.Editing;
using Windows.Media.Playback;
using System.Collections.ObjectModel;
using System.Threading;
using Microsoft.Graphics.Canvas;
using Windows.Media.Effects;
using Windows.Foundation.Collections;
using Windows.Media.MediaProperties;
using Windows.UI.Popups;


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
		//StorageFile horizonFile;
		private MediaComposition composition;
		private MediaPlayer mediaPlayer;
		private Rect rect = new Rect(0, 0, 4096, 2048);
		MediaClip video;
        public CancellationTokenSource tokenSource = new CancellationTokenSource();
		public CancellationToken token;

		bool dotsFlag = false;
		bool horizonFlag = false;
		bool forceFovFlag = false;

		int forcedFov = 0;
		public SavingResolutionsCollection resolutions;

		private ObservableCollection<Session> _items = new ObservableCollection<Session>();

		public ObservableCollection<Session> Items
		{
			get { return this._items; }
		}

		public MainPage()
		{
			this.InitializeComponent();
			//InitializeFrostedGlass(mediaPlayerElement);
			//InitializeDropShadow(mainPanel, previewImage);

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
			colorRect.Background = new SolidColorBrush(sender.Color);
			Debug.WriteLine("videoBackgroundPicker: " + videoBackgroundPicker.Color);
		}

		private void ColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
		{
			Debug.WriteLine("Rect: " + sender.Color);
			videoBackgroundPicker.Color = sender.Color;
			//videoBackgroundPicker_ColorChanged(sender, args);
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

			HideHeatmapGenerating();
		}

		private async void VideoGenTest2(object sender, RoutedEventArgs e)
		{

            var ep = SetVideoPlayer();
			//var ep = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
			//ep.Video.Height = 600;
			//ep.Video.Width = 1200;

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

            FillEffectPropertySet(sessionCollection);


            if (mediaPlayer == null)
			{
				mediaPlayer = new MediaPlayer();
			}

			var video = await MediaClip.CreateFromFileAsync(videoFile);

			MediaOverlayLayer videoOverlayLayer = new MediaOverlayLayer();
			TrimVideo(ref video);
			valuePairs.Add("offset", video.TrimTimeFromStart.Ticks);
			var enc = video.GetVideoEncodingProperties();

			valuePairs.Add("frameLength", (1 / ((double)enc.FrameRate.Numerator / enc.FrameRate.Denominator)) * 1000);

			composition.Clips.Add(video);

			if (horizonFlag)
			{
				composition.OverlayLayers.Add(await GenerateHorizonLayer((int)video.TrimmedDuration.TotalSeconds, ep.Video.Height, ep.Video.Width));			
			}

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

        private MediaEncodingProfile SetVideoPlayer()
        {
            MediaEncodingProfile result;

            uint width = (uint)mediaPlayerElement.ActualWidth;
            uint height = (uint)mediaPlayerElement.ActualHeight;

            if (width % 2 != 0)
            {
                width = width - 1;
            }

            if (height % 2 != 0)
            {
                height = height - 1;
            }

            var enc = video.GetVideoEncodingProperties();

            if (width/height == enc.Width/enc.Height)
            {
                result = GetMediaEncoding(new Resolutions(new SavingResolutions { height = height, width = width }, false), enc);
                mediaPlayerElement.Width = width;
                mediaPlayerElement.Height = height;
            }
            else
            {
                double temp = (double)enc.Width / enc.Height;
                uint newHeight = (uint)(width / temp);

                if (newHeight % 2 != 0)
                {
                    newHeight = newHeight - 1;
                }

                result = GetMediaEncoding(new Resolutions(new SavingResolutions { width = width, height = newHeight }, false), enc);
                mediaPlayerElement.Width = width;
                mediaPlayerElement.Height = newHeight;
            }

            //mediaPlayerElement.Width = width;
            //result.Video.Height = height;
            //result.Video.Width = width;

            return result;

        }

		private async void SaveVideo_Click(object sender, RoutedEventArgs e)
		{

			tokenSource.Dispose();
			tokenSource = new CancellationTokenSource();
			token = tokenSource.Token;

            var temp = saveResolutionSelector.SelectedItem as Resolutions;
            var enc = video.GetVideoEncodingProperties();

            MediaEncodingProfile mediaEncoding = GetMediaEncoding(temp, enc);
    
			Debug.WriteLine("Vid type: " + enc.Type);
			Debug.WriteLine("Vid sub: " + enc.Subtype);
			Debug.WriteLine("Vid id: " + enc.ProfileId);

			var picker = new Windows.Storage.Pickers.FileSavePicker();
			picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
			picker.FileTypeChoices.Add("MP4 files", new List<string>() { ".mp4" });
			picker.SuggestedFileName = "RenderedVideo.mp4";
			saveProgressCallback saveProgress = ShowErrorMessage;

			Windows.Storage.StorageFile file = await picker.PickSaveFileAsync();
			if (file != null)
			{
				mediaPlayer.Pause();

				valuePairs.Remove("height");
				valuePairs.Remove("width");

				valuePairs.Add("height", temp.Resolution.height);
				valuePairs.Add("width", temp.Resolution.width);

				if (dotsFlag)
				{
					valuePairs.Remove("dotsRadius");
					valuePairs.Add("dotsRadius", (float)temp.Resolution.width / 4096 *20);
				}

				if (horizonFlag)
				{
					composition.OverlayLayers[0] = await GenerateHorizonLayer((int)video.TrimmedDuration.TotalSeconds, temp.Resolution.height, temp.Resolution.width);
				}

				buttonLoadingStop.Visibility = Visibility.Visible;
				generateVideoButton.IsEnabled = false;
				saveCompositionButton.IsEnabled = false;

				StaticHeatmapGenerator.RenderCompositionToFile(file, composition, saveProgress, Window.Current, mediaEncoding, token, saveResolutionSelector.SelectedItem);

			}
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
			
			rangeSelector.Maximum = time.TotalMilliseconds;
			rangeSelector.Minimum = 0;
			rangeSelector.RangeMax = rangeSelector.Maximum;
			rangeSelector.RangeMin = rangeSelector.Minimum;

			double rangemax = rangeSelector.RangeMax / 1000;

			int stopHour = (int)rangemax / 3600;
			int stopMinute = (int)(rangemax - stopHour * 3600) / 60;
			int stopSecond = (int)(rangemax - stopMinute * 60);
			int stopMili = (int)((rangemax - stopMinute * 60 - stopSecond) * 1000);

			TimeSpan stopTime = new TimeSpan(0, stopHour, stopMinute, stopSecond, stopMili);

			timeRangeStart.Text = "00:00:00";
			timeRangeStop.Text = stopTime.ToString(@"m\:ss\:fff");

			var enc = video.GetVideoEncodingProperties();

			rangeSelector.StepFrequency = 1 / ((double)enc.FrameRate.Numerator / enc.FrameRate.Denominator) *1000;


			//videoStartSlider.Maximum = time.TotalSeconds;
			//videoStopSlider.Maximum = time.TotalSeconds;
			//videoStopSlider.Value = videoStopSlider.Maximum;
		}

		private void TrimVideo(ref MediaClip video)
		{
			//int start = (int)videoStartSlider.Value;
			//int stop = (int)videoStopSlider.Value;

			int start = (int)rangeSelector.RangeMin;
			int stop = (int)rangeSelector.RangeMax;

			video.TrimTimeFromStart = new TimeSpan(0, 0, 0,0, start);
			video.TrimTimeFromEnd = new TimeSpan(0, 0, 0, 0, (int)(video.OriginalDuration.TotalMilliseconds - stop));

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


			if (forceFovFlag)
			{
				GetForcedFov();
			}

			List<Heatmap.Coord []> test = new List<Heatmap.Coord[]>();

			foreach (Session s in sessions.sessions)
			{
				test.Add(interpolateSession(s, enc.FrameRate.Numerator, enc.FrameRate.Denominator, video.OriginalDuration));
			}
			long framesCount = enc.FrameRate.Numerator * video.OriginalDuration.Ticks / TimeSpan.TicksPerSecond / enc.FrameRate.Denominator;



			//tu dodać try/catch 
			for (int i = 0; i < framesCount; i++)
            {
                for (int j = 0; j < sessions.sessions.Count; j++)
                {
                    try
                    {
                        pitch.Add(test[j][i].pitch);
                        yaw.Add(test[j][i].yaw);
                        fov.Add(test[j][i].fov);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        pitch.Add(0);
                        yaw.Add(0);
                        fov.Add(0);
                    }
                    //fov.Insert
                }

            }


            float dotsRadius = (float)mediaPlayerElement.ActualWidth / 4096 * 20;


            valuePairs.Add("backgroundColor", videoBackgroundPicker.Color);
			valuePairs.Add("backgroundOpacity", (float)(1 - videoOpacity.Value / 100));
			valuePairs.Add("dotsRadius", dotsRadius);
            valuePairs.Add("count", sessions.sessions.Count);
			valuePairs.Add("pitch", pitch);
			valuePairs.Add("yaw", yaw);
			valuePairs.Add("fov", fov);
			valuePairs.Add("generateDots", dotsFlag);
			valuePairs.Add("heatmapOpacity", (float)(heatmapOpacity.Value / 100));
			

			valuePairs.Add("height", enc.Height);
			valuePairs.Add("width", enc.Width);
		}
		
		private Heatmap.Coord[] interpolateSession (Session session, uint videoFrameNumerator, uint videoFrameDenominator, TimeSpan videoDuration)
		{
			long framesCount = videoFrameNumerator * videoDuration.Ticks / TimeSpan.TicksPerSecond / videoFrameDenominator;
			Heatmap.Coord[] interpolated = new Heatmap.Coord[framesCount];

			int coordsLength;
			List<Heatmap.Coord> coords = Heatmap.CoordsDeserialize(session.history);
			coordsLength = coords.Count;

			float inOutProportion = (float)session.Length.Ticks / videoDuration.Ticks;
			if (inOutProportion > 1)
			{
				inOutProportion = 1;
			}


			int lastFramePosition = (int)(inOutProportion * (framesCount-1)); //represenst the value last position of last coord in new interpolated array

			float origTransformationStep = (float)lastFramePosition / (coordsLength-1); //represents the value of the transformation step coords[x*k] -> inteprpolated[x + origTransformationStep*k] [-2 because first and last position are set manually below]


            //fill array with known values
            for (int i = 0; i < coordsLength; i++)
			{
				float temp = i * origTransformationStep;
				int newPosition = (int)Math.Round(temp, 0);

				interpolated[newPosition] = coords[i];

                if (forceFovFlag)
                {
                    if (interpolated[newPosition].fov != 0)
                    {
                        interpolated[newPosition].fov = forcedFov;
                    }
                }
            }

            //fill rest of time with empty heatmaps
            for (int i = lastFramePosition+1; i <interpolated.Length; i++)
            {
                interpolated[i] = new Heatmap.Coord { fov = 0, pitch = 0, yaw = 0 };
            }

            int originalFrameCount = 1;
            for (int i = 0; i < lastFramePosition-1; i++)
            {
                int interpolationFromIndex = i;
                Heatmap.Coord valueFrom = interpolated[i];

                int interpolationToIndex = (int)Math.Round((originalFrameCount) * origTransformationStep);
                Heatmap.Coord valueTo = interpolated[interpolationToIndex];

                int interpolationLength = interpolationToIndex - i - 1;

                if (interpolationLength == 0)
                {
                    continue;
                }

                float yawStepValue = (valueTo.yaw - valueFrom.yaw) / (float)interpolationLength;
                float pitchStepValue = (valueTo.pitch - valueFrom.pitch) / (float)interpolationLength;

                int yawLength = valueTo.yaw - valueFrom.yaw;
                if (Math.Abs(yawLength) > 31)
                {
                    yawStepValue = (64 - Math.Abs(yawLength)) / (float)interpolationLength;
                }

                int internalCounter = 1;
                while (interpolationLength > 0)
                {
                    
                    i++;
                    interpolated[i] = new Heatmap.Coord();
                    interpolated[i].fov = interpolated[interpolationFromIndex].fov;
                    interpolated[i].pitch = (int)Math.Round(interpolated[interpolationFromIndex].pitch + pitchStepValue*internalCounter, 0);
                    interpolated[i].yaw = (int)Math.Round(interpolated[interpolationFromIndex].yaw + yawStepValue*internalCounter, 0) % 64;

                    if (Math.Abs(yawLength) > 31 && valueFrom.yaw < valueTo.yaw)
                    {
                        interpolated[i].yaw = ((int)Math.Round(interpolated[interpolationFromIndex].yaw - yawStepValue*internalCounter, 0) + 10*64) % 64;
                    }
                    interpolationLength--;
                    internalCounter++;
                }
                originalFrameCount++;

            }

			return interpolated;
		}
		
		private void dotsEnableCheckbox_Checked(object sender, RoutedEventArgs e)
		{
			dotsFlag = true;
		}

		private void dotsEnableCheckbox_Unchecked(object sender, RoutedEventArgs e)
		{
			dotsFlag = false;
		}

		private async Task<MediaOverlayLayer> GenerateHorizonLayer(int timeInSeconds, uint height, uint width)
		{

			CanvasBitmap cb = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), new Uri("ms-appx:///Assets/horizon3840x2160.png"));
			//cb.AlphaMode = CanvasAlphaMode.Straight;
			//img.Source = horizonFile;

			MediaOverlayLayer horizonOverlay = new MediaOverlayLayer();


			MediaOverlay mediaOverlay = new MediaOverlay(MediaClip.CreateFromSurface(cb, new TimeSpan(0, 0, timeInSeconds))); //generowanie horyzontu
			mediaOverlay.Position = new Rect(0, 0, width, height);
			mediaOverlay.Opacity = 1;


			horizonOverlay.Overlays.Add(mediaOverlay);

			return horizonOverlay;
		}

        private MediaEncodingProfile GetMediaEncoding (Resolutions resolution, VideoEncodingProperties videoEncoding)
        {
            MediaEncodingProfile mediaEncoding;

            if (resolution.Resolution.width >= 2560)
            {
                mediaEncoding = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Uhd2160p);
            }
            else if (resolution.Resolution.width <= 1280)
            {
                mediaEncoding = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
            }
            else
            {
                mediaEncoding = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
            }

            mediaEncoding.Video.FrameRate.Denominator = videoEncoding.FrameRate.Denominator;
            mediaEncoding.Video.FrameRate.Numerator = videoEncoding.FrameRate.Numerator;


            mediaEncoding.Video.Width = resolution.Resolution.width;
            mediaEncoding.Video.Height = resolution.Resolution.height;

            long inputVideo = videoEncoding.Width * videoEncoding.Height;
            long outputVideo = resolution.Resolution.width * resolution.Resolution.height;

            mediaEncoding.Video.Bitrate = (uint)(videoEncoding.Bitrate * outputVideo / inputVideo);

            return mediaEncoding;
        }

		private void forceFovCheckbox_Checked(object sender, RoutedEventArgs e)
		{
			forceFovFlag = true;
		}

		private void forceFovCheckbox_Unchecked(object sender, RoutedEventArgs e)
		{
			forceFovFlag = false;
		}

		private async void GetForcedFov ()
		{
			if (int.TryParse(forcedFovTextBox.Text, out forcedFov))
			{
				if (forcedFov > 19 && forcedFov < 181)
				{
					var dialog = new MessageDialog("Forced fov set correctly to " + forcedFov);
					dialog.Title = "Ok";
					dialog.Commands.Add(new UICommand { Label = "OK", Id = 0 });
					await dialog.ShowAsync();
				}
				else
				{
					forcedFov = 75;
					var dialog = new MessageDialog("Value should be in 20-180 range.");
					dialog.Title = "Warning";
					dialog.Commands.Add(new UICommand { Label = "OK", Id = 0 });
					await dialog.ShowAsync();					
				}
			}
			else
			{
				forcedFov = 75;
				var dialog = new MessageDialog("Please set the value in 20-180 range.");
				dialog.Title = "Warning";
				dialog.Commands.Add(new UICommand { Label = "OK", Id = 0 });
				await dialog.ShowAsync();
			}
		}

		private void rangeSelector_ValueChanged(object sender, Microsoft.Toolkit.Uwp.UI.Controls.RangeChangedEventArgs e)
		{
			int stopHour = (int)rangeSelector.RangeMax / 3600;
			int stopMinute = (int)(rangeSelector.RangeMax - stopHour * 3600) / 60;
			int stopSecond = (int)(rangeSelector.RangeMax - stopMinute * 60);
			int stopMili = (int)((rangeSelector.RangeMax - stopMinute * 60 - stopSecond) * 1000);

			TimeSpan stopTime = new TimeSpan(0, stopHour, stopMinute, stopSecond, stopMili);

			int startHour = (int)rangeSelector.RangeMin / 3600;
			int startMinute = (int)rangeSelector.RangeMin / 60;
			int startSecond = (int)(rangeSelector.RangeMin - startMinute * 60);
			int startMili = (int)((rangeSelector.RangeMin - startMinute * 60 - startSecond) * 1000);

			TimeSpan startTime = new TimeSpan(0, startHour, startMinute, startSecond, startMili);

			string format = @"h\:mm\:ss\:fff";
			if (stopHour == 0 && startHour == 0)
			{
				format = @"m\:ss\:fff";
			}

			timeRangeStart.Text = startTime.ToString(format);
			timeRangeStop.Text = stopTime.ToString(format);

			Debug.WriteLine(rangeSelector.RangeMin);
			Debug.WriteLine(rangeSelector.RangeMax);

			//Debug.WriteLine(playerGrid.ActualWidth);
			Debug.WriteLine(playerGrid.ActualWidth);

		}

		private void rangeSelector_PointerMoved(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
		{
			double rangemax = rangeSelector.RangeMax / 1000;
			double rangemin = rangeSelector.RangeMin / 1000;

			int stopHour = (int)rangemax / 3600;
			int stopMinute = (int)(rangemax - stopHour * 3600) / 60;
			int stopSecond = (int)(rangemax - stopMinute * 60);
			int stopMili = (int)((rangemax - stopMinute * 60 - stopSecond) * 1000);

			TimeSpan stopTime = new TimeSpan(0, stopHour, stopMinute, stopSecond, stopMili);

			int startHour = (int)rangemin / 3600;
			int startMinute = (int)rangemin / 60;
			int startSecond = (int)(rangemin - startMinute * 60);
			int startMili = (int)((rangemin - startMinute * 60 - startSecond) * 1000);

			TimeSpan startTime = new TimeSpan(0, startHour, startMinute, startSecond, startMili);

			string format = @"h\:mm\:ss\:fff";
			if (stopHour == 0 && startHour == 0)
			{
				format = @"m\:ss\:fff";
			}

			timeRangeStart.Text = startTime.ToString(format);
			timeRangeStop.Text = stopTime.ToString(format);
		}

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            forcedFovTextBox.Text = "75";
        }
    }


}

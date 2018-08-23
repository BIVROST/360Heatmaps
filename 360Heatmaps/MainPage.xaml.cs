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
	public delegate void SaveProgressCallback(double message);
	public sealed partial class MainPage : Page
	{
		public PropertySet ValuePairs = new PropertySet();
		StorageFile _videoFile;
		//StorageFile horizonFile;
		private MediaComposition _composition;
		private MediaPlayer _mediaPlayer;
		private Rect _rect = new Rect(0, 0, 4096, 2048);
		MediaClip _video;
        public CancellationTokenSource TokenSource = new CancellationTokenSource();
		public CancellationToken Token;

		bool _dotsFlag = false;
		bool _horizonFlag = false;
		bool _forceFovFlag = false;
		bool _grayscaleVideoFlag = false;
		bool _scaleFovFlag = false;

		int _forcedFov = 0;
		int _scaleFovInPercentage = 0;
		public SavingResolutionsCollection Resolutions;

		private ObservableCollection<Session> _items = new ObservableCollection<Session>();

		public ObservableCollection<Session> Items => this._items;

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
			
			
			Token = TokenSource.Token;
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
			IReadOnlyList<StorageFile> files;

			files = await LoadFile("single", ".mp4");

			if (files.Count != 0)
			{
				file = files[0];
				if (file.ContentType == "video/mp4")
				{
					_videoFile = file;
					selectedVideoFileTextBlock.Text = _videoFile.DisplayName;
					_video = await MediaClip.CreateFromFileAsync(_videoFile);
					SetTimeSliders(_video.OriginalDuration);
					GenerateButtonEnable();
					var enc = _video.GetVideoEncodingProperties();
					Resolutions = new SavingResolutionsCollection(enc);
					saveResolutionSelector.ItemsSource = Resolutions;
					saveResolutionSelector.SelectedIndex = 0;

					AllowToWatchVideoBeforeGenerating(true);
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
						_videoFile = file;
						selectedVideoFileTextBlock.Text = _videoFile.DisplayName;
						_video = await MediaClip.CreateFromFileAsync(_videoFile);
						SetTimeSliders(_video.OriginalDuration);
						GenerateButtonEnable();
						var enc = _video.GetVideoEncodingProperties();
						Resolutions = new SavingResolutionsCollection(enc);
						saveResolutionSelector.ItemsSource = Resolutions;
						saveResolutionSelector.SelectedIndex = 0;

						AllowToWatchVideoBeforeGenerating(true);
					}
				}
			}
			GenerateButtonEnable();
		}

		private void AllowToWatchVideoBeforeGenerating (bool allowFlag)
		{
			MediaComposition comp = new MediaComposition();
			comp.Clips.Add(_video);

			mediaPlayerElement.Source = null;
			mediaPlayerElement.AreTransportControlsEnabled = true;
			var ep = SetVideoPlayer();

			if (_mediaPlayer == null)
			{
				_mediaPlayer = new MediaPlayer();
			}

			var res = comp.GenerateMediaStreamSource(ep);
			var md = MediaSource.CreateFromMediaStreamSource(res);
			mediaPlayerElement.Source = md;
			_mediaPlayer = mediaPlayerElement.MediaPlayer;
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

		private async Task<IReadOnlyList<StorageFile>> LoadFile (string openType, params string[] fileTypes)
		{
			FileOpenPicker openPicker = new FileOpenPicker();
			IReadOnlyList<StorageFile> files = new List<StorageFile>(1);
			StorageFile file;

			openPicker.ViewMode = PickerViewMode.Thumbnail;
			openPicker.SuggestedStartLocation = PickerLocationId.Desktop;

			foreach (String s in fileTypes)
			{
				openPicker.FileTypeFilter.Add(s);
			}

			if (openType.Equals("single"))
			{
				file = await openPicker.PickSingleFileAsync();
				files = new List<StorageFile>
				{
					file
				};
			}
			else if (openType.Equals("multiple"))
			{
				files = await openPicker.PickMultipleFilesAsync();
			}


			return files;

		}

		private async void addHeatmaps_Click(object sender, RoutedEventArgs e)
		{
			//StorageFile file;
			IReadOnlyList<StorageFile> files;

			ShowHeatmapLoading();

			files = await LoadFile("multiple", SessionCollection.SupportedExtensions);	
			
			
			if (files.Count != 0)
			{
				foreach (StorageFile file in files)
				{
					await AddItemsFromFileAsync(file);
				}
			}

			HideHeatmapLoading();
			
		}

		private async Task AddItemsFromFileAsync(StorageFile file)
		{
			SessionCollection sc = await SessionCollection.FromFileAsync(file);
			if (sc == null)
			{
				await new MessageDialog("Could not load file " + file.Name).ShowAsync();
				return;
			}

			foreach (Session s in sc.sessions)
			{
				Items.Add(s);
			}
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
						if (SessionCollection.IsSupportedFileExtension(file.FileType))
						{
							await AddItemsFromFileAsync(file);
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
			_horizonFlag = true;
		}

		private void horizonEnableCheckbox_Unchecked(object sender, RoutedEventArgs e)
		{
			_horizonFlag = false;
		}

		private async void GenerateStaticHeatmap(object sender, RoutedEventArgs e)
		{

			mediaPlayerElement.Source = null;
			saveCompositionButton.IsEnabled = false;
			mediaPlayerElement.AreTransportControlsEnabled = false;

			mediaPlayerElement.Width = 1200;
			mediaPlayerElement.Height = 600;

			ShowHeatmapGenerating();
			

			SessionCollection sessionCollection = new SessionCollection();
			sessionCollection.sessions = new List<Session>();


			var listItems = heatmapSessionsListView.SelectedItems.ToList();
			foreach (Session s in listItems)
			{
				sessionCollection.sessions.Add(s);
			}

			if (_forceFovFlag)
			{
				GetForcedFov();
			}

			HeatmapGenerator generator = new HeatmapGenerator();

			var result = await generator.GenerateHeatmap
						  (
						  _scaleFovFlag,
						  _scaleFovInPercentage,
						  _forceFovFlag,
						  _forcedFov,
						  _horizonFlag,
						  sessionCollection,
						  _rect,
						  videoBackgroundPicker.Color,
						  heatmapOpacity.Value / 100,
						  rangeSelector.RangeMin,
						  rangeSelector.RangeMax,
						  _video
						  );
			
		

			if (_mediaPlayer == null)
			{
				_mediaPlayer = new MediaPlayer();
			}
			_mediaPlayer = mediaPlayerElement.MediaPlayer;

			mediaPlayerElement.Source = MediaSource.CreateFromMediaStreamSource(result);
			HideHeatmapGenerating();
		}

		private async void VideoGenTest2(object sender, RoutedEventArgs e)
		{

            var ep = SetVideoPlayer();

			saveCompositionButton.IsEnabled = false;
			_composition = new MediaComposition();
			mediaPlayerElement.Source = null;
			SessionCollection sessionCollection = new SessionCollection();
			sessionCollection.sessions = new List<Session>();


			var listItems = heatmapSessionsListView.SelectedItems.ToList();
			foreach (Session s in listItems)
			{
				sessionCollection.sessions.Add(s);
			}

			HeatmapGenerator generator = new HeatmapGenerator();

			generator.CheckHistoryErrors(sessionCollection);

            FillEffectPropertySet(sessionCollection);


            if (_mediaPlayer == null)
			{
				_mediaPlayer = new MediaPlayer();
			}

			var video = await MediaClip.CreateFromFileAsync(_videoFile);

			MediaOverlayLayer videoOverlayLayer = new MediaOverlayLayer();
			TrimVideo(ref video);
			ValuePairs.Add("offset", video.TrimTimeFromStart.Ticks);
			var enc = video.GetVideoEncodingProperties();

			ValuePairs.Add("frameLength", (1 / ((double)enc.FrameRate.Numerator / enc.FrameRate.Denominator)) * 1000);

			_composition.Clips.Add(video);

			if (_horizonFlag)
			{
				_composition.OverlayLayers.Add(await GenerateHorizonLayer((int)video.TrimmedDuration.TotalSeconds, ep.Video.Height, ep.Video.Width));			
			}

			var videoEffectDefinition = new VideoEffectDefinition("VideoEffectComponent.HeatmapAddVideoEffect", ValuePairs);
			video.VideoEffectDefinitions.Add(videoEffectDefinition);

			MediaStreamSource res;
			try
			{

				ValuePairs.Remove("height");
				ValuePairs.Remove("width");

				ValuePairs.Add("height", ep.Video.Height);
				ValuePairs.Add("width", ep.Video.Width);

				res = _composition.GenerateMediaStreamSource(ep);
				var md = MediaSource.CreateFromMediaStreamSource(res);
				mediaPlayerElement.Source = md;
			}
			catch (Exception f)
			{
				Debug.WriteLine(f.Message);
			}

			_mediaPlayer = mediaPlayerElement.MediaPlayer;
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

            var enc = _video.GetVideoEncodingProperties();

            if (width/height == enc.Width/enc.Height)
            {
                result = GetMediaEncoding(new Resolutions(new SavingResolutions { Height = height, Width = width }, false), enc);
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

                result = GetMediaEncoding(new Resolutions(new SavingResolutions { Width = width, Height = newHeight }, false), enc);
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

			TokenSource.Dispose();
			TokenSource = new CancellationTokenSource();
			Token = TokenSource.Token;

            var temp = saveResolutionSelector.SelectedItem as Resolutions;
            var enc = _video.GetVideoEncodingProperties();

            MediaEncodingProfile mediaEncoding = GetMediaEncoding(temp, enc);
    
			Debug.WriteLine("Vid type: " + enc.Type);
			Debug.WriteLine("Vid sub: " + enc.Subtype);
			Debug.WriteLine("Vid id: " + enc.ProfileId);

			var picker = new Windows.Storage.Pickers.FileSavePicker();
			picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
			picker.FileTypeChoices.Add("MP4 files", new List<string>() { ".mp4" });
			picker.SuggestedFileName = "RenderedVideo.mp4";
			SaveProgressCallback saveProgress = ShowErrorMessage;

			Windows.Storage.StorageFile file = await picker.PickSaveFileAsync();
			if (file != null)
			{
				_mediaPlayer.Pause();

				ValuePairs.Remove("height");
				ValuePairs.Remove("width");

				ValuePairs.Add("height", temp.Resolution.Height);
				ValuePairs.Add("width", temp.Resolution.Width);

				if (_dotsFlag)
				{
					ValuePairs.Remove("dotsRadius");
					ValuePairs.Add("dotsRadius", (float)temp.Resolution.Width / 4096 *20);
				}

				if (_horizonFlag)
				{
					_composition.OverlayLayers[0] = await GenerateHorizonLayer((int)_video.TrimmedDuration.TotalSeconds, temp.Resolution.Height, temp.Resolution.Width);
				}

				//buttonLoadingStop.Visibility = Visibility.Visible;
				generateVideoButton.IsEnabled = false;
				saveCompositionButton.IsEnabled = false;

				HeatmapGenerator generator = new HeatmapGenerator();

				generator.RenderCompositionToFile(file, _composition, saveProgress, Window.Current, mediaEncoding, Token, saveResolutionSelector.SelectedItem);

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
				buttonLoadingStop.Visibility = Visibility.Visible;
				videoLoading.Value = v;
			}
		}

		private void GenerateButtonEnable ()
		{
			if (_videoFile != null && heatmapSessionsListView.SelectedItems.Count > 0)
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

			var enc = _video.GetVideoEncodingProperties();

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
			ShowErrorMessage(100.0);
			TokenSource.Cancel();
        }

		private void FillEffectPropertySet (SessionCollection sessions)
		{
            ValuePairs.Clear();
		

			var enc = _video.GetVideoEncodingProperties();


			var pitch = new List<int>();
			var yaw = new List<int>();
			var fov = new List<int>();


			if (_forceFovFlag)
			{
				GetForcedFov();
			}

			List<Heatmap.Coord []> test = new List<Heatmap.Coord[]>();

			foreach (Session s in sessions.sessions)
			{
				test.Add(InterpolateSession(s, enc.FrameRate.Numerator, enc.FrameRate.Denominator, _video.OriginalDuration));
			}
			long framesCount = enc.FrameRate.Numerator * _video.OriginalDuration.Ticks / TimeSpan.TicksPerSecond / enc.FrameRate.Denominator;



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
	                catch (Exception)
	                {
						;
	                }

	                //fov.Insert
                }

            }


            float dotsRadius = (float)mediaPlayerElement.ActualWidth / 4096 * 20;

			ValuePairs.Add("grayscaleVideoFlag", _grayscaleVideoFlag);
            ValuePairs.Add("backgroundColor", videoBackgroundPicker.Color);
			ValuePairs.Add("backgroundOpacity", (float)(1 - videoOpacity.Value / 100));
			ValuePairs.Add("dotsRadius", dotsRadius);
            ValuePairs.Add("count", sessions.sessions.Count);
			ValuePairs.Add("pitch", pitch);
			ValuePairs.Add("yaw", yaw);
			ValuePairs.Add("fov", fov);
			ValuePairs.Add("generateDots", _dotsFlag);
			ValuePairs.Add("heatmapOpacity", (float)(heatmapOpacity.Value / 100));
			

			ValuePairs.Add("height", enc.Height);
			ValuePairs.Add("width", enc.Width);
		}
		
		private Heatmap.Coord[] InterpolateSession (Session session, uint videoFrameNumerator, uint videoFrameDenominator, TimeSpan videoDuration)
		{
			long framesCount = videoFrameNumerator * videoDuration.Ticks / TimeSpan.TicksPerSecond / videoFrameDenominator;
			Heatmap.Coord[] interpolated = new Heatmap.Coord[framesCount];

			int coordsLength;
			List<Heatmap.Coord> coords = Heatmap.CoordsDeserialize(session.history);


			float inOutProportion = (float)session.Length.Ticks / videoDuration.Ticks;
			if (inOutProportion > 1)
			{
				List<Heatmap.Coord> temp = coords.GetRange(0,
					(int) (videoDuration.Ticks / TimeSpan.TicksPerSecond) * session.sample_rate); 
				inOutProportion = 1;
				coords = temp;
			}

			coordsLength = coords.Count;
			int lastFramePosition = (int)(inOutProportion * (framesCount-1)); //represenst the value last position of last coord in new interpolated array

			float origTransformationStep = (float)lastFramePosition / (coordsLength-1); //represents the value of the transformation step coords[x*k] -> inteprpolated[x + origTransformationStep*k] [-2 because first and last position are set manually below]


            //fill array with known values
            for (int i = 0; i < coordsLength; i++)
			{
				float temp = i * origTransformationStep;
				int newPosition = (int)Math.Round(temp, 0);

				interpolated[newPosition] = coords[i];

                if (_forceFovFlag)
                {
                    if (interpolated[newPosition].fov != 0)
                    {
                        interpolated[newPosition].fov = _forcedFov;
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
			_dotsFlag = true;
		}

		private void dotsEnableCheckbox_Unchecked(object sender, RoutedEventArgs e)
		{
			_dotsFlag = false;
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

            if (resolution.Resolution.Width >= 2560)
            {
                mediaEncoding = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Uhd2160p);
            }
            else if (resolution.Resolution.Width <= 1280)
            {
                mediaEncoding = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
            }
            else
            {
                mediaEncoding = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
            }

            mediaEncoding.Video.FrameRate.Denominator = videoEncoding.FrameRate.Denominator;
            mediaEncoding.Video.FrameRate.Numerator = videoEncoding.FrameRate.Numerator;


            mediaEncoding.Video.Width = resolution.Resolution.Width;
            mediaEncoding.Video.Height = resolution.Resolution.Height;

            long inputVideo = videoEncoding.Width * videoEncoding.Height;
            long outputVideo = resolution.Resolution.Width * resolution.Resolution.Height;

            mediaEncoding.Video.Bitrate = (uint)(videoEncoding.Bitrate * outputVideo / inputVideo);

            return mediaEncoding;
        }

		private void forceFovCheckbox_Checked(object sender, RoutedEventArgs e)
		{
			scaleFovCheckbox.IsChecked = false;
			scaleFovCheckbox.IsEnabled = false;
			_forceFovFlag = true;
			_scaleFovFlag = false;
		}

		private void forceFovCheckbox_Unchecked(object sender, RoutedEventArgs e)
		{
			scaleFovCheckbox.IsEnabled = true;
			_forceFovFlag = false;
		}

		private async void GetForcedFov ()
		{

			if (int.TryParse(forcedFovTextBox.Text, out _forcedFov))
			{
				if (_forcedFov > -1 && _forcedFov < 181)
				{
					var dialog = new MessageDialog("Forced fov set correctly to " + _forcedFov);
					dialog.Title = "Ok";
					dialog.Commands.Add(new UICommand { Label = "OK", Id = 0 });
					await dialog.ShowAsync();
				}
				else
				{
					_forcedFov = 90;
					var dialog = new MessageDialog("Value should be in 0-180 range.");
					dialog.Title = "Warning";
					dialog.Commands.Add(new UICommand { Label = "OK", Id = 0 });
					await dialog.ShowAsync();
				}
			}
			else
			{
				_forcedFov = 90;
				var dialog = new MessageDialog("Please set the value in 0-180 range.");
				dialog.Title = "Warning";
				dialog.Commands.Add(new UICommand { Label = "OK", Id = 0 });
				await dialog.ShowAsync();
			}
		}

		private void rangeSelector_ValueChanged(object sender, Microsoft.Toolkit.Uwp.UI.Controls.RangeChangedEventArgs e)
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

		private void resetScaleFov_Click(object sender, RoutedEventArgs e)
		{
			scaleFovSlider.Value = 100;
		}

		private void grayscaleVideo_Checked(object sender, RoutedEventArgs e)
		{
			_grayscaleVideoFlag = true;
		}

		private void grayscaleVideo_Unchecked(object sender, RoutedEventArgs e)
		{
			_grayscaleVideoFlag = false;
		}

		private void scaleFovCheckbox_Checked(object sender, RoutedEventArgs e)
		{
			forceFovCheckbox.IsChecked = false;
			forceFovCheckbox.IsEnabled = false;
			_scaleFovFlag = true;
			_forceFovFlag = false;
		}

		private void scaleFovCheckbox_Unchecked(object sender, RoutedEventArgs e)
		{
			forceFovCheckbox.IsEnabled = true;
			_scaleFovFlag = false;
		}

		private void scaleFovSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
		{
			_scaleFovInPercentage = (int)(sender as Slider).Value;
		}
	}


}

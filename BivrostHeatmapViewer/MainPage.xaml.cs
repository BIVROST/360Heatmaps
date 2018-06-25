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

		StorageFile videoFile;
		StorageFile horizonFile;
		private MediaComposition composition;
		private MediaComposition mementoComposition;
		private MediaPlayer mediaPlayer;
		private Rect rect = new Rect(0, 0, 1280, 720);
		MediaClip video;
        public CancellationTokenSource tokenSource = new CancellationTokenSource();
		public CancellationToken token;
		Task<MediaOverlayLayer> task;
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
                                ;
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

		private async void horizonEnableCheckbox_Checked(object sender, RoutedEventArgs e)
		{
			if (composition != null)
			{
				horizonFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri ("ms-appx:///Assets/horizon3840x2160.png"));

				mementoComposition = composition.Clone();

				MediaOverlayLayer horizonOverlay = new MediaOverlayLayer();
				MediaOverlay mediaOverlay = new MediaOverlay(await MediaClip.CreateFromImageFileAsync(horizonFile, composition.Duration));//generowanie horyzontu
				mediaOverlay.Position = rect;
				mediaOverlay.Opacity = 0.7;

				horizonOverlay.Overlays.Add(mediaOverlay);
				composition.OverlayLayers.Add(horizonOverlay);

				var res = composition.GeneratePreviewMediaStreamSource(1280, 720);
				var md = MediaSource.CreateFromMediaStreamSource(res);
				mediaPlayerElement.Source = md;
			}
			//horizonImage.Opacity = 0.7;
			//horizonImage.Visibility = Visibility.Visible;	
		}

		private void horizonEnableCheckbox_Unchecked(object sender, RoutedEventArgs e)
		{
			if (composition != null && mementoComposition != null)
			{
				composition = mementoComposition.Clone();
				var res = composition.GeneratePreviewMediaStreamSource(1280, 720);
				var md = MediaSource.CreateFromMediaStreamSource(res);
				mediaPlayerElement.Source = md;
			}//horizonImage.Visibility = Visibility.Collapsed;
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

			task = StaticHeatmapGenerator.GenerateVideoFromHeatmap
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

		private async void SaveVideo_Click(object sender, RoutedEventArgs e)
		{
            var dialog = new MessageDialog("That operation cannot be canceled.");
            dialog.Title = "Are you sure?";
            dialog.Commands.Add(new UICommand { Label = "OK", Id = 0 });
            dialog.Commands.Add(new UICommand { Label = "Cancel", Id = 1 });
            var idResult = await dialog.ShowAsync();

            if ((int)idResult.Id == 0)
            {

                var picker = new Windows.Storage.Pickers.FileSavePicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
                picker.FileTypeChoices.Add("MP4 files", new List<string>() { ".mp4" });
                picker.SuggestedFileName = "RenderedVideo.mp4";
                saveProgressCallback saveProgress = ShowErrorMessage;

                Windows.Storage.StorageFile file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    generateVideoButton.IsEnabled = false;
                    saveCompositionButton.IsEnabled = false;
                    StaticHeatmapGenerator.RenderCompositionToFile(file, composition, saveProgress, Window.Current);
                }
            }
		}

		private void ShowErrorMessage(double v)
		{
			if (Double.Equals(v, 100.0))
			{
				videoLoading.Visibility = Visibility.Collapsed;
				loadingScreen.Visibility = Visibility.Collapsed;
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

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			mediaPlayer.Play();
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
            tokenSource.Cancel();
        }

		private void videoStopSlider_DragEnter(object sender, DragEventArgs e)
		{
			if (videoStopSlider.Value <= videoStartSlider.Value)
			{
				videoStopSlider.Value = videoStopSlider.Maximum;
			}
		}

		public static byte[] RenderHeatmap()
		{
			byte[] image = new byte[64 * 64 * 4];

			for (int it = 0; it < 8; it++)
			{
				var c = Microsoft.Toolkit.Uwp.Helpers.ColorHelper.FromHsl(360, 1, 0.5);
				image[it * 4 + 0] = c.B;
				image[it * 4 + 1] = c.G;
				image[it * 4 + 2] = c.R;
				image[it * 4 + 3] = 255;
			}

			image[0] = 0;
			image[1] = 0;
			image[2] = 0;
			image[3] = 0;

			return image;
		}

		private void Page_Unloaded(object sender, RoutedEventArgs e)
		{
			//this.canvas.RemoveFromVisualTree();
			//this.canvas = null;
		}


	}


}

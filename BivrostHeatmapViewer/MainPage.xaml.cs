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


//camera capture
using Windows.Media.Capture;
using Windows.ApplicationModel;
using System.Threading.Tasks;
using Windows.System.Display;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using System.Diagnostics;



// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BivrostHeatmapViewer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
		StorageFile videoFile;
		WriteableBitmap wb;

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
				//heatImage.Source = wb;
			});
		}

		public MainPage()
        {
            this.InitializeComponent();
			

			InitializeFrostedGlass(heatImage);
			InitializeDropShadow(mainPanel, previewImage);
			pickFolderButton.Click += pickFolderButton_Click;
			

			heatmapSessionsListView.SelectionChanged += (s, e) =>
			{
				previewButton.IsEnabled = heatmapSessionsListView.SelectedItems.Count > 0;
			};
			previewButton.IsEnabled = heatmapSessionsListView.SelectedItems.Count > 0;

			previewButton.Click += PreviewButton_Click;

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

		private void PreviewButton_Click(object sender, RoutedEventArgs e)
		{
			if (heatmapSessionsListView.SelectedItems.Count <= 0) return;

			if(heatmapSessionsListView.SelectedItems.Count == 1)
			{

			} else
			{

			}

		}

		private async void ListFolderButton_Click(object sender, RoutedEventArgs e)
		{
			StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Sessions/session01.bvr"));
			var json = await Windows.Storage.FileIO.ReadTextAsync(file);
			var session = JsonConvert.DeserializeObject<Session>(json);

			var deserializedData = Heatmap.CoordsDeserialize(session.history);

			deserializedData.Clear();
			deserializedData.Add(new Heatmap.Coord() { yaw = 32, pitch = 32, fov = 45 });

			var heatmap = Heatmap.Generate(deserializedData);
			Preview(Heatmap.RenderHeatmap(heatmap), 64, 64);


			Stopwatch w1 = new Stopwatch();
			w1.Start();

			
			for (int it = 0; it < 1000; it++)
			{
				deserializedData = Heatmap.CoordsDeserialize(session.history);
				heatmap = Heatmap.Generate(deserializedData);
				var imageMap = Heatmap.RenderHeatmap(heatmap);
			}
			w1.Stop();

			System.Diagnostics.Debug.WriteLine(1000f / (w1.ElapsedMilliseconds / 1000f));
			fpsLabel.Text = (1000f / (w1.ElapsedMilliseconds / 1000f)).ToString();
			;

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
			dropShadow.Color = Color.FromArgb(255, 75, 75, 80);
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
						mediaPlayer.Source = MediaSource.CreateFromStorageFile(videoFile);
					}
				}
			}
		}

		private void playButton_Click(object sender, RoutedEventArgs e)
		{
			mediaPlayer.MediaPlayer.Play();
			
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{

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
				var items = heatmapSessionsListView.SelectedItems.ToList();
				foreach (var i in items)
					heatmapSessionsListView.Items.Remove(i);
			}
		}


		private async void addHeatmaps_Click(object sender, RoutedEventArgs e)
		{
			StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Sessions/sessions-okrutna.js"));
			var json = await Windows.Storage.FileIO.ReadTextAsync(file);
			var sessionCollection = JsonConvert.DeserializeObject<SessionCollection>(json);

			heatmapSessionsListView.Items.Add(new HeatmapListItem(sessionCollection.sessions[0]));
		}

		private void ShowHeatmapLoading()
		{
			heatmapListLoadingIndicator.IsActive = true;
			heatmapListLoadingScreenGrid.Visibility = Visibility.Visible;
		}

		private void HideHeatmapLoading()
		{
			heatmapListLoadingScreenGrid.Visibility = Visibility.Collapsed;
			heatmapListLoadingIndicator.IsActive = false;
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
	}
}

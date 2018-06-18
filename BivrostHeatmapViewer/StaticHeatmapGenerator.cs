using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace BivrostHeatmapViewer
{
	public class StaticHeatmapGenerator
	{
		public static async Task<MediaStreamSource> GenerateHeatmap(Session session, Rect overlayPosition, ColorPicker colorPicker, double heatmapOpacity)
		{
			MediaComposition mediaComposition = new MediaComposition();
			MediaOverlayLayer overlayLayer = new MediaOverlayLayer();
			MediaStreamSource mediaStreamSource;

			var deserializedData = Heatmap.CoordsDeserialize(session.history);
			var heatmap = Heatmap.Generate(deserializedData);
			var renderedHeatmap = Heatmap.RenderHeatmap(heatmap);

			WriteableBitmap wb = new WriteableBitmap(64, 64);


			using (Stream stream = wb.PixelBuffer.AsStream())
			{
				await stream.WriteAsync(renderedHeatmap, 0, renderedHeatmap.Length);
			}

			var clip = await MediaClip.CreateFromImageFileAsync(await WriteableBitmapToStorageFile(wb), new TimeSpan(0, 0, 0, 0, 1));
			
			var background = MediaClip.CreateFromColor(colorPicker.Color, new TimeSpan(0, 0, 0, 0, 1));

			mediaComposition.Clips.Add(background);

			MediaOverlay mediaOverlay = new MediaOverlay(clip);
			mediaOverlay.Position = overlayPosition;
			mediaOverlay.Opacity = heatmapOpacity;

			overlayLayer.Overlays.Add(mediaOverlay);
			mediaComposition.OverlayLayers.Add(overlayLayer);

			mediaStreamSource = mediaComposition.GeneratePreviewMediaStreamSource
				(
				(int)overlayPosition.Width,
				(int)overlayPosition.Height
				); 

			return mediaStreamSource;
		}


		public static async Task<MediaStreamSource> GenerateHeatmap(SessionCollection sessions, Rect overlayPosition, ColorPicker colorPicker, double heatmapOpacity)
		{
			StringBuilder builder = new StringBuilder();

			CheckHistoryErrors(sessions);

			foreach (Session s in sessions.sessions)
				builder.Append(s.history);

			Session session = new Session();
			session.history = builder.ToString();

			return await GenerateHeatmap(session, overlayPosition, colorPicker, heatmapOpacity);
		}


        private static async Task<MediaOverlay> Test(List<Heatmap.Coord> coords, Rect overlayPosition, ColorPicker colorPicker, double heatmapOpacity, int sampleRate)
        {

            //MediaComposition mediaComposition = new MediaComposition();
            MediaOverlayLayer overlayLayer = new MediaOverlayLayer();
            //MediaStreamSource mediaStreamSource;

            //var deserializedData = Heatmap.CoordsDeserialize(session.history);
            var heatmap = Heatmap.Generate(coords);
            var renderedHeatmap = Heatmap.RenderHeatmap(heatmap);

            WriteableBitmap wb = new WriteableBitmap(64, 64);


            using (Stream stream = wb.PixelBuffer.AsStream())
            {
                await stream.WriteAsync(renderedHeatmap, 0, renderedHeatmap.Length);
            }

            var clip = await MediaClip.CreateFromImageFileAsync(await WriteableBitmapToStorageFile(wb), new TimeSpan(0, 0, 0, 0, 1000/sampleRate));

            //var background = MediaClip.CreateFromColor(colorPicker.Color, new TimeSpan(0, 0, 0, 0, 100));

            //mediaComposition.Clips.Add(background);

            MediaOverlay mediaOverlay = new MediaOverlay(clip);
            mediaOverlay.Position = overlayPosition;
            mediaOverlay.Opacity = heatmapOpacity;

			//overlayLayer.Overlays.Add(mediaOverlay);
			//mediaComposition.OverlayLayers.Add(overlayLayer);

			//mediaStreamSource = mediaComposition.GenerateMediaStreamSource();
			/* (
			 (int)overlayPosition.Width,
			 (int)overlayPosition.Height
			 );
			 */
            return mediaOverlay;
        }

        public static async Task<MediaOverlayLayer> GenerateVideoFromHeatmap(CancellationToken token, SessionCollection sessions, Rect overlayPosition, ColorPicker colorPicker, ProgressBar videoGeneratingProgress, 
			double opacity, Slider videoStartSlider, Slider videoStopSlider)
        {

			await Windows.Storage.ApplicationData.Current.ClearAsync();
			List<MediaStreamSource> mediaStreamSources = new List<MediaStreamSource>();

			CheckHistoryErrors(sessions);

			Session session = sessions.sessions[0];
			List<MediaOverlay> mediaOverlays = new List<MediaOverlay>();

            List<Heatmap.Coord>[] coordsArray = new List<Heatmap.Coord>[sessions.sessions.Count];
            coordsArray[0] = Heatmap.CoordsDeserialize(sessions.sessions[0].history);

            int min_length = coordsArray[0].Count - 1;

            for (int i = 1; i < sessions.sessions.Count; i++)
            {
                coordsArray[i] = Heatmap.CoordsDeserialize(sessions.sessions[i].history);

                if (min_length > coordsArray[i].Count)
                {
                    min_length = coordsArray[i].Count;
                }
            }

            List<Heatmap.Coord>[] generatedCoords = new List<Heatmap.Coord>[min_length];
            for (int i = 0; i < min_length; i++)
            {
                generatedCoords[i] = new List<Heatmap.Coord>();
            }

            int sampleRate;

            if (!CheckSampleRate(sessions, out sampleRate))
            {
                var dialog = new MessageDialog("Sessions should have the same sample rate. Please change selected sessions.");
                dialog.Title = "Error";
                dialog.Commands.Add(new UICommand { Label = "OK", Id = 0 });
                await dialog.ShowAsync();
                return new MediaOverlayLayer();
            }



			int startValue = (int)videoStartSlider.Value * sampleRate;
			int stopValue = (int)videoStopSlider.Value * sampleRate;

			if (stopValue > min_length)
			{
				stopValue = min_length;
			}

			videoGeneratingProgress.Maximum = stopValue - startValue;
			videoGeneratingProgress.Visibility = Visibility.Visible;

			try
			{
				for (int i = startValue; i < stopValue; i++)
				{
					for (int j = 0; j < sessions.sessions.Count; j++)
					{
						generatedCoords[i].Add(coordsArray[j][i]);
					}
					if (token.IsCancellationRequested)
					{
						token.ThrowIfCancellationRequested();
					}
					mediaOverlays.Add(await Test(generatedCoords[i], overlayPosition, colorPicker, 0.35, sampleRate));

					videoGeneratingProgress.Value = i - startValue;
				}
			}
			catch (OperationCanceledException e)
			{
				return new MediaOverlayLayer();
			}
			catch (Exception e)
			{
				Debug.WriteLine(e.Message);
			}
			finally
			{
				videoGeneratingProgress.Value = 0;
				videoGeneratingProgress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				//token.IsCancellationRequested 
				//token = CancellationToken.None;
			}

			int delay = 0;
			MediaOverlayLayer mediaOverlayLayer = new MediaOverlayLayer();
			foreach (MediaOverlay x in mediaOverlays)
			{
				x.Delay = new TimeSpan(0, 0, 0, 0, delay);
				delay += 1000/sampleRate;
				x.Opacity = opacity;
				mediaOverlayLayer.Overlays.Add(x);
			}

            /*
            Console.WriteLine();
            
            List<Heatmap.Coord> coords = Heatmap.CoordsDeserialize(session.history);
            mediaStreamSources.Add(await Test(coords, overlayPosition, colorPicker, 0.8));

            
               List<Heatmap.Coord> coords2 = Heatmap.CoordsDeserialize(session.history);
               mediaStreamSources.Add(await Test(coords2, overlayPosition, colorPicker, 0.8));


               List<Heatmap.Coord> coords3 = Heatmap.CoordsDeserialize(session.history);
               mediaStreamSources.Add(await Test(coords3, overlayPosition, colorPicker, 0.8));

               List<Heatmap.Coord> coords4 = new List<Heatmap.Coord>();
               coords4.AddRange(coords);
               coords4.AddRange(coords2);
               mediaStreamSources.Add(await Test(coords4, overlayPosition, colorPicker, 0.8));
               */

            return mediaOverlayLayer;
		}

		public static void RenderCompositionToFile(StorageFile file, MediaComposition composition, saveProgressCallback ShowErrorMessage, Window window)
		{
				// Call RenderToFileAsync
				var saveOperation = composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise);

				saveOperation.Progress = new AsyncOperationProgressHandler<TranscodeFailureReason, double>(async (info, progress) =>
				{
					await window.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
					{
						ShowErrorMessage(progress);
					}));
				});
				saveOperation.Completed = new AsyncOperationWithProgressCompletedHandler<TranscodeFailureReason, double>(async (info, status) =>
				{
					await window.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
					{
						try
						{
							var results = info.GetResults();
							if (results != TranscodeFailureReason.None || status != AsyncStatus.Completed)
							{
								//ShowErrorMessage("Saving was unsuccessful");
							}
							else
							{
								//ShowErrorMessage("Trimmed clip saved to file");
							}
						}
						finally
						{
							// Update UI whether the operation succeeded or not
						}

					}));
				});

			//button.IsEnabled = true;
			//await saveOperation;
		}


		public static async Task<StorageFile> WriteableBitmapToStorageFile(WriteableBitmap WB)
		{
			string FileName = "MyFile.";
			Guid BitmapEncoderGuid = BitmapEncoder.JpegEncoderId;
			FileName += "tiff";
			BitmapEncoderGuid = BitmapEncoder.TiffEncoderId; //7s

			var file = await Windows.Storage.ApplicationData.Current.TemporaryFolder
				.CreateFileAsync(FileName, CreationCollisionOption.GenerateUniqueName);

			using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
			{
				BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoderGuid, stream);
				Stream pixelStream = WB.PixelBuffer.AsStream();
				byte[] pixels = new byte[pixelStream.Length];
				await pixelStream.ReadAsync(pixels, 0, pixels.Length);
				encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
						  (uint)WB.PixelWidth,
						  (uint)WB.PixelHeight,
						  96.0,
						  96.0,
						  pixels);
				await encoder.FlushAsync();
			}
			return file;
		}

        private static bool CheckSampleRate (SessionCollection sessions, out int sampleRate)
        {
            sampleRate = sessions.sessions[0].sample_rate;
            foreach (Session x in sessions.sessions)
            {
                if (sampleRate != x.sample_rate)
                {
                    return false;
                }
            }

            return true;
        }

		private static async void CheckHistoryErrors (SessionCollection sessions)
		{
			bool flag;

			foreach (Session s in sessions.sessions)
			{
				if (s.history.Contains("--"))
				{
					flag = true;
				}
				else
				{
					flag = false;
				}

				if (flag)
				{
					var dialog = new MessageDialog("Sessions contains time errors. Added empty heatmaps to repair it.");
					dialog.Title = "Error";
					dialog.Commands.Add(new UICommand { Label = "OK", Id = 0 });
					await dialog.ShowAsync();
				}

			}
		}

	}

}

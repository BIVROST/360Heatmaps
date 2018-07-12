using Microsoft.Graphics.Canvas;
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
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace BivrostHeatmapViewer
{



	public class StaticHeatmapGenerator
	{
		//MemoryCache

		//private static Dictionary<Heatmap.Coord, WriteableBitmap> wbCache = new Dictionary<Heatmap.Coord, WriteableBitmap>(new HeatmapComparer());

		private static async Task<WriteableBitmap> GenerateHeatmap(Session session)
		{
			var deserializedData = Heatmap.CoordsDeserialize(session.history);
			var heatmap = Heatmap.Generate(deserializedData);
			var renderedHeatmap = Heatmap.RenderHeatmap(heatmap);

			WriteableBitmap wb = new WriteableBitmap(64, 64);


			using (Stream stream = wb.PixelBuffer.AsStream())
			{
				await stream.WriteAsync(renderedHeatmap, 0, renderedHeatmap.Length);
			}

			return wb;
		}

		private static async Task<WriteableBitmap> GenerateHeatmap(Heatmap.Coord coord)
		{
			//lock (wbCache)
			//{
			//	if (wbCache.ContainsKey(coord))
			//	{
			//		return wbCache[coord];
			//	}
			//}

			List<Heatmap.Coord> coords = new List<Heatmap.Coord>();
			coords.Add(coord);

			var heatmap = Heatmap.Generate(coords);
			var renderedHeatmap = Heatmap.RenderHeatmap(heatmap);

			WriteableBitmap wb = new WriteableBitmap(64, 64);

			SoftwareBitmap sb = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 64, 64);

			using (Stream stream = wb.PixelBuffer.AsStream())
			{
				await stream.WriteAsync(renderedHeatmap, 0, renderedHeatmap.Length);

			}

			try
			{
				wb = wb.Resize(2048, 1080, WriteableBitmapExtensions.Interpolation.Bilinear);
				//lock (wbCache)
				//{
				//	wbCache.TryAdd(coord, wb);
				//}
				return wb;
			}
			catch (OutOfMemoryException)
			{
				return new WriteableBitmap(2048, 1080);
			}


		}

		public static async Task<MediaStreamSource> GenerateHeatmap(SessionCollection sessions, Rect overlayPosition, ColorPicker colorPicker, double heatmapOpacity)
		{
			CheckHistoryErrors(sessions);

			StringBuilder sb = new StringBuilder();
			MediaOverlayLayer mediaOverlayLayer = new MediaOverlayLayer();
			WriteableBitmap wb;// = new List<WriteableBitmap>();

			foreach (Session x in sessions.sessions)
			{
				sb.Append(x.history);
			}

			Session s = new Session();
			s.history = sb.ToString();

			wb = await GenerateHeatmap(s);

			/*

			foreach (Session s in sessions.sessions)
			{
				wb.Add(await GenerateHeatmap(s, overlayPosition));
				//heatmap.Opacity = heatmapOpacity / sessions.sessions.Count;
				//mediaOverlayLayer.Overlays.Add(heatmap);
			}


			for (int i = 1; i < sessions.sessions.Count; i++)
			{
				wb[0].Blit(overlayPosition, wb[i], overlayPosition, WriteableBitmapExtensions.BlendMode.Alpha);
			}

			//wb[0].Blit(overlayPosition, wb[1], overlayPosition, WriteableBitmapExtensions.BlendMode.Alpha);
			var clip = await MediaClip.CreateFromImageFileAsync(await WriteableBitmapToStorageFile(wb[0]), new TimeSpan(0, 0, 0, 0, 1));
			*/

			CanvasDevice device = CanvasDevice.GetSharedDevice();

			SoftwareBitmap swb = SoftwareBitmap.CreateCopyFromBuffer(wb.PixelBuffer, BitmapPixelFormat.Bgra8, wb.PixelWidth, wb.PixelHeight);
			swb = SoftwareBitmap.Convert(swb, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);

			CanvasBitmap canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(device, swb);
			


			//var a = MediaClip.CreateFromSurface(canvasBitmap, new TimeSpan(0, 0, 0, 0, 1));

			//offscreen
			

			


			var clip = MediaClip.CreateFromSurface(canvasBitmap, new TimeSpan(0, 0, 0, 0, 1));

			//CanvasComposition canvasComposition = CanvasComposition.Cr



			//clip = MediaClip.CreateFromSurface(surface, new TimeSpan(0, 0, 0, 0, 1));



			//MediaClip.

			MediaOverlay mediaOverlay = new MediaOverlay(clip);
			mediaOverlay.Position = overlayPosition;
			mediaOverlay.Opacity = heatmapOpacity;

			mediaOverlayLayer.Overlays.Add(mediaOverlay);


			MediaComposition mediaComposition = new MediaComposition();
			mediaComposition.Clips.Add(MediaClip.CreateFromColor(colorPicker.Color, new TimeSpan(0, 0, 0, 0, 1)));
			mediaComposition.OverlayLayers.Add(mediaOverlayLayer);

			return mediaComposition.GeneratePreviewMediaStreamSource
				(
				(int)overlayPosition.Width,
				(int)overlayPosition.Height
				);

		}


		private static async Task<MediaOverlay> GenerateVideoHeatmap2 (List<Heatmap.Coord> coords, Rect overlayPosition, ColorPicker colorPicker, double heatmapOpacity, int sampleRate, bool generateDots = false)
		{
			var heatmap = Heatmap.Generate(coords);
			var renderedHeatmap = Heatmap.RenderHeatmap(heatmap);

			WriteableBitmap wb;

			Stopwatch s1 = new Stopwatch();
			s1.Start();

			wb = await GenerateHeatmap(coords[0]);
			
			
			if (generateDots)
			{
				foreach (Heatmap.Coord x in coords)
				{

					wb.FillEllipseCentered(x.yaw * 2048 / 64, x.pitch * 1080 / 64, 16, 16, Colors.Black);
					wb.FillEllipseCentered(x.yaw * 2048 / 64, x.pitch * 1080 / 64, 17, 17, Colors.Black);
					wb.FillEllipseCentered(x.yaw * 2048 / 64, x.pitch * 1080 / 64, 15, 15, Colors.DarkOrange);
				}
			}
			s1.Stop();
			Debug.WriteLine("Generowanie bitmapy z resize: " + s1.Elapsed.TotalMilliseconds.ToString() + "ms");
			s1.Reset();
			s1.Start();

			CanvasDevice device = CanvasDevice.GetSharedDevice();

			SoftwareBitmap swb = SoftwareBitmap.CreateCopyFromBuffer(wb.PixelBuffer, BitmapPixelFormat.Bgra8, wb.PixelWidth, wb.PixelHeight);
			swb = SoftwareBitmap.Convert(swb, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);

			MediaOverlay mediaOverlay;
			using (CanvasBitmap canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(device, swb))
			{
				mediaOverlay = new MediaOverlay(MediaClip.CreateFromSurface(canvasBitmap, new TimeSpan(0, 0, 0, 0, 1)));
			}

			//var clip = await MediaClip.CreateFromImageFileAsync(await WriteableBitmapToStorageFile(wb), new TimeSpan(0, 0, 0, 0, 1000 / sampleRate));
			s1.Stop();
			Debug.WriteLine("Bitmapa -> storagefile: " + s1.Elapsed.TotalMilliseconds.ToString() + "ms");

			///MediaOverlay mediaOverlay = new MediaOverlay(clip);
			mediaOverlay.Position = overlayPosition;
			mediaOverlay.Opacity = heatmapOpacity;

			return mediaOverlay;
		}


		public static async Task<MediaOverlayLayer> GenerateVideoFromHeatmap2(CancellationToken token, SessionCollection sessions, Rect overlayPosition, ColorPicker colorPicker, ProgressBar videoGeneratingProgress,
			double opacity, Slider videoStartSlider, Slider videoStopSlider, bool generateDots)
		{

			await Windows.Storage.ApplicationData.Current.ClearAsync();
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
					mediaOverlays.Add(await GenerateVideoHeatmap2(generatedCoords[i], overlayPosition, colorPicker, 0.35, sampleRate, generateDots));

					videoGeneratingProgress.Value = i - startValue;
				}
			}
			catch (OperationCanceledException e)
			{
				Debug.WriteLine(e.Message);
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
			}

			int delay = 0;
			MediaOverlayLayer mediaOverlayLayer = new MediaOverlayLayer();
			foreach (MediaOverlay x in mediaOverlays)
			{
				x.Delay = new TimeSpan(0, 0, 0, 0, delay);
				delay += 1000 / sampleRate;
				x.Opacity = opacity;
				mediaOverlayLayer.Overlays.Add(x);
			}

			return mediaOverlayLayer;
		}
		
		public static void RenderCompositionToFile(StorageFile file, MediaComposition composition, saveProgressCallback ShowErrorMessage, Window window, MediaEncodingProfile encodingProfile, CancellationToken token, object selectedResolution)
		{

			// Call RenderToFileAsync

			//encodingProfile.Video.FrameRate.Denominator = 1001;
			//encodingProfile.Video.FrameRate.Numerator = 30000;

			var temp = selectedResolution as Resolutions;

			encodingProfile.Video.Height = temp.Resolution.height;
			encodingProfile.Video.Width = temp.Resolution.width;
			//encodingProfile.Video.Bitrate = 1000000000;


			Debug.WriteLine((double)encodingProfile.Video.FrameRate.Numerator / encodingProfile.Video.FrameRate.Denominator);

			var saveOperation = composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise, encodingProfile);

			saveOperation.Progress = new AsyncOperationProgressHandler<TranscodeFailureReason, double>(async (info, progress) =>
			{
				await window.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
				{
					ShowErrorMessage(progress);
					try
					{
						if (token.IsCancellationRequested)
						{
							saveOperation.Cancel();
							ShowErrorMessage(100.0);
							//token.ThrowIfCancellationRequested();
						}
					}
					catch (OperationCanceledException)
					{
					}
				}));
			});

			saveOperation.Completed = new AsyncOperationWithProgressCompletedHandler<TranscodeFailureReason, double>(async (info, status) =>
				{
					await window.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
					{
						if (saveOperation.Status != AsyncStatus.Canceled)
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
							catch (Exception e)
							{
								Debug.WriteLine("Saving exception: " + e.Message);
							}
							finally
							{
								// Update UI whether the operation succeeded or not
							}
						}
					}));
				});
		}
		
		public static bool CheckSampleRate (SessionCollection sessions, out int sampleRate)
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

		public static async void CheckHistoryErrors (SessionCollection sessions)
		{
			bool flag = false;

			foreach (Session s in sessions.sessions)
			{
				if (s.history.Contains("--"))
				{
					flag = true;
					break;
				}
				else
				{
					flag = false;
				}
			}

			if (flag)
			{
				var dialog = new MessageDialog("Sessions contains time errors. Added empty heatmaps to repair it.");
				dialog.Title = "Warning";
				dialog.Commands.Add(new UICommand { Label = "OK", Id = 0 });
				await dialog.ShowAsync();
			}
		}



	}

}

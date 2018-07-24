using Microsoft.Graphics.Canvas;
using System;
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
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace BivrostHeatmapViewer
{



	public class StaticHeatmapGenerator
	{
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

		public static void RenderCompositionToFile(StorageFile file, MediaComposition composition, saveProgressCallback ShowErrorMessage, Window window, MediaEncodingProfile encodingProfile, CancellationToken token, object selectedResolution)
		{

			//var temp = selectedResolution as Resolutions;

			//encodingProfile.Video.Height = temp.Resolution.height;
			//encodingProfile.Video.Width = temp.Resolution.width;

			Debug.WriteLine("Save type: " + encodingProfile.Video.Type);
			Debug.WriteLine("Save sub: " + encodingProfile.Video.Subtype);
			Debug.WriteLine("Save id: " + encodingProfile.Video.ProfileId);
			Debug.WriteLine("numerator: " + encodingProfile.Video.FrameRate.Numerator + " denominator: " + encodingProfile.Video.FrameRate.Denominator);
			Debug.WriteLine((double)encodingProfile.Video.FrameRate.Numerator / encodingProfile.Video.FrameRate.Denominator);

			//encodingProfile.Video.Bitrate = 12_000_000;

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
                                ShowErrorMessage(100.0);
							}
							finally
							{
								// Update UI whether the operation succeeded or not
							}
						}
					}));
				});
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

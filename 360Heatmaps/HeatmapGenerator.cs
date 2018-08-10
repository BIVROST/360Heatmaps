using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace BivrostHeatmapViewer
{



	public class HeatmapGenerator
	{

		private async Task<WriteableBitmap> GenerateHeatmap(List<Heatmap.Coord> inputList, bool forceFov, int forcedFov, bool scaleFovFlag, int scaleInPercentage)
		{
			//var deserializedData = Heatmap.CoordsDeserialize(session.history);

			if (forceFov)
			{
				foreach (Heatmap.Coord h in inputList)
				{
					if (h.fov == 0)
					{
						continue;
					}
					h.fov = forcedFov;
				}
			}
			else if (scaleFovFlag)
			{
				foreach (Heatmap.Coord h in inputList)
				{
					h.fov = ScaleFov(h.fov, scaleInPercentage);
				}
			}

			float[] heatmap = await Task.Factory.StartNew(() =>
				Heatmap.Generate(inputList)
			);

			var renderedHeatmap = Heatmap.RenderHeatmap(heatmap);

			WriteableBitmap wb = new WriteableBitmap(64, 64);


			using (Stream stream = wb.PixelBuffer.AsStream())
			{
				await stream.WriteAsync(renderedHeatmap, 0, renderedHeatmap.Length);
			}

			return wb;
		}

		private List<Heatmap.Coord> TrimStaticHeatmap(SessionCollection sessions, double startTime, double stopTime, MediaClip video)
		{
			List<Heatmap.Coord> inputList = new List<Heatmap.Coord>();
			if (video != null)
			{
				foreach (Session x in sessions.sessions)
				{
					int fps = x.sample_rate;
					int start = (int)Math.Floor(startTime / 1000 * fps);
					int stop = (int)Math.Floor(stopTime / 1000 * fps);

					var deserial = Heatmap.CoordsDeserialize(x.history);

					for (int i = start; i < stop; i++)
					{
						try
						{
							inputList.Add(deserial[i]);
						}
						catch
						{
							inputList.Add(new Heatmap.Coord { fov = 0, pitch = 0, yaw = 0 });
						}
					}

				}
			}
			else
			{
				foreach (Session x in sessions.sessions)
				{
					var deserial = Heatmap.CoordsDeserialize(x.history);
					inputList.AddRange(deserial);
				}
			}

			return inputList;
		}

		public async Task<MediaStreamSource> GenerateHeatmap(bool scaleFovFlag, int scaleInPercentage, bool forceFov, int forcedFov, bool horizonFlag, SessionCollection sessions, Rect overlayPosition, Windows.UI.Color colorPickerColor,
			double heatmapOpacity, double startTime, double stopTime, MediaClip video)
		{

			CheckHistoryErrors(sessions);

			List<Heatmap.Coord> inputList = await Task.Factory.StartNew(() =>
				 TrimStaticHeatmap(sessions, startTime, stopTime, video)
			);

			MediaOverlayLayer mediaOverlayLayer = new MediaOverlayLayer();
			WriteableBitmap wb = await GenerateHeatmap(inputList, forceFov, forcedFov, scaleFovFlag, scaleInPercentage);



			CanvasDevice device = CanvasDevice.GetSharedDevice();

			SoftwareBitmap swb = SoftwareBitmap.CreateCopyFromBuffer(wb.PixelBuffer, BitmapPixelFormat.Bgra8, wb.PixelWidth, wb.PixelHeight);
			swb = SoftwareBitmap.Convert(swb, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);

			CanvasBitmap canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(device, swb);


			var clip = MediaClip.CreateFromSurface(canvasBitmap, new TimeSpan(0, 0, 0, 0, 1));

			MediaOverlay mediaOverlay = new MediaOverlay(clip)
			{
				Position = overlayPosition,
				Opacity = heatmapOpacity
			};

			mediaOverlayLayer.Overlays.Add(mediaOverlay);


			if (horizonFlag)
			{
				CanvasBitmap cb = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), new Uri("ms-appx:///Assets/horizon3840x2160.png"));

				MediaOverlay horizonOverlay =
					new MediaOverlay(MediaClip.CreateFromSurface(cb, new TimeSpan(0, 0, 0, 0, 1)))
					{
						Position = overlayPosition,
						Opacity = 1
					};


				mediaOverlayLayer.Overlays.Add(horizonOverlay);

			}



			MediaComposition mediaComposition = new MediaComposition();

			mediaComposition.Clips.Add(MediaClip.CreateFromColor(colorPickerColor, new TimeSpan(0, 0, 0, 0, 1)));
			mediaComposition.OverlayLayers.Add(mediaOverlayLayer);


			return mediaComposition.GeneratePreviewMediaStreamSource
				(
				(int)overlayPosition.Width,
				(int)overlayPosition.Height
				);

			

		}

		public void RenderCompositionToFile(StorageFile file, MediaComposition composition, SaveProgressCallback showErrorMessage, Window window, MediaEncodingProfile encodingProfile, CancellationToken token, object selectedResolution)
		{
			
			var saveOperation = composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise, encodingProfile);

			saveOperation.Progress = async (info, progress) =>
			{
				await window.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
				{
					showErrorMessage(progress);
					try
					{
						if (token.IsCancellationRequested)
						{
							saveOperation.Cancel();
							showErrorMessage(100.0);
						}
					}
					catch (OperationCanceledException)
					{
					}
				});
			};

			saveOperation.Completed = async (info, status) =>
			{
				await window.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
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
								await Launcher.LaunchFileAsync(file);
							}
						}
						catch (Exception e)
						{
							Debug.WriteLine("Saving exception: " + e.Message);
							var dialog = new MessageDialog("Saving exception: " + e.Message);
							dialog.Title = "Error";
							dialog.Commands.Add(new UICommand { Label = "OK", Id = 0 });
							await dialog.ShowAsync();
							showErrorMessage(100.0);

						}
					}
				});
			};
			
		}
		
		public async void CheckHistoryErrors (SessionCollection sessions)
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
				var dialog =
					new MessageDialog("Sessions contains time errors. Added empty heatmaps to repair it.")
					{
						Title = "Warning"
					};
				dialog.Commands.Add(new UICommand { Label = "OK", Id = 0 });
				await dialog.ShowAsync();
			}
		}

		private int ScaleFov (int inputFov, int scaleInPercent)
		{
			double scale = (double)scaleInPercent / 100;
			var outputFov = (int)Math.Round((double)inputFov * scale, 0);

			if (outputFov > 180)
			{
				return 180;
			}
			else if (outputFov < 0)
			{
				return 0;
			}


			return outputFov;
		}

	}

}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.Streams;
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

			var clip = await MediaClip.CreateFromImageFileAsync(await WriteableBitmapToStorageFile(wb, FileFormat.Tiff), new TimeSpan(0, 0, 0, 0, 1));
			
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

			foreach (Session s in sessions.sessions)
				builder.Append(s.history);

			Session session = new Session();
			session.history = builder.ToString();

			return await GenerateHeatmap(session, overlayPosition, colorPicker, heatmapOpacity);
		}



        public static async Task<List<MediaStreamSource>> GenerateVideoFromHeatmap(SessionCollection sessions, Rect overlayPosition, ColorPicker colorPicker)
        {
            WriteableBitmap wb = new WriteableBitmap(64, 64);
            List<byte[]> heatmaps = new List<byte[]>();
            MediaComposition composition = new MediaComposition();
            List<MediaStreamSource> mediaStreamSource = new List<MediaStreamSource>();
            List<List<Heatmap.Coord>> coords = new List<List<Heatmap.Coord>>();
            List<List<Heatmap.Coord>> heatmapCoord = new List<List<Heatmap.Coord>>();

            int min_length = 0;

            coords.Add(Heatmap.CoordsDeserialize(sessions.sessions[0].history));
            min_length = coords[0].Count;


            for (int i = 1; i < sessions.sessions.Count - 1; i++)
            {
                coords.Add(Heatmap.CoordsDeserialize(sessions.sessions[i].history));
                if (min_length > coords[i].Count)
                    min_length = coords[i].Count;
            }

            for (int i = 0; i < min_length - 1; i++)
            {
                heatmapCoord.Add(new List<Heatmap.Coord>());
                for (int k = 0; k < sessions.sessions.Count - 1; k++)
                {
                    heatmapCoord[i].Add(coords[k][i]);
                }
            }

            List<byte[]> pixels = new List<byte[]>();

            for (int i = 0; i < min_length - 1; i++)
            {
                pixels.Add(Heatmap.RenderHeatmap(Heatmap.Generate(heatmapCoord[i])));
            }


            for (int i = 0; i < min_length - 1; i++)
            {
                using (Stream stream = wb.PixelBuffer.AsStream())
                {
                    await stream.WriteAsync(pixels[i], 0, pixels[i].Length);
                }

                var clip = await MediaClip.CreateFromImageFileAsync(await WriteableBitmapToStorageFile(wb, FileFormat.Tiff), new TimeSpan(0, 0, 0, 0, 1));

                var background = MediaClip.CreateFromColor(colorPicker.Color, new TimeSpan(0, 0, 0, 0, 1));

                composition.Clips.Add(background);
                mediaStreamSource.Add(composition.GeneratePreviewMediaStreamSource
                    (
                    (int)overlayPosition.Width,
                    (int)overlayPosition.Height
                    )
            );
            }



            return mediaStreamSource;

          //  return pixels;

			//int count = heatmapSessionsListView.Items.Count;

			for (int i = 1; i < 5 - 1; i++)
			{
				StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Sessions/session0" + i + ".bvr"));
				var json = await Windows.Storage.FileIO.ReadTextAsync(file);
				var session = JsonConvert.DeserializeObject<Session>(json);

				var deserializedData = Heatmap.CoordsDeserialize(session.history);
				var heatmap = Heatmap.Generate(deserializedData);
				//byte[] pixels = Heatmap.RenderHeatmap(heatmap);
				//heatmaps.Add(pixels);
			}
		}
/*
			var clip = await MediaClip.CreateFromFileAsync(videoFile);

			MediaOverlay videoOverlay = new MediaOverlay(clip);
			videoOverlay.Position = overlayPosition;
			videoOverlay.Opacity = 0.7;
			videoOverlay.AudioEnabled = true;

			var bR = colorPicker.Color.R;
			var bG = colorPicker.Color.G;
			var bB = colorPicker.Color.B;
			Windows.UI.Color videoBackgoundColor = Windows.UI.Color.FromArgb(255, bR, bG, bB);

			var videoBackground = MediaClip.CreateFromColor(videoBackgoundColor, new TimeSpan(0, 0, 20));

			composition.Clips.Add(videoBackground);


			//composition.Clips.Add(await MediaClip.CreateFromImageFileAsync(await WriteableBitmapToStorageFile(wb, FileFormat.Png), new TimeSpan(0, 0, 3)));

			mediaStreamSource = composition.GeneratePreviewMediaStreamSource(
				(int)overlayPosition.Width,
				(int)overlayPosition.Height
				);


			MediaOverlay mediaOverlay;


			MediaOverlayLayer mediaOverlayLayer = new MediaOverlayLayer();
			mediaOverlayLayer.Overlays.Add(videoOverlay);

			TimePicker timePicker = new TimePicker();

			//Stopwatch stopwatch = new Stopwatch();
			//stopwatch.Start();

			//videoLoading.Visibility = Visibility.Visible;

			for (int i = 1; i < 200; i++)
			{
				using (Stream stream = wb.PixelBuffer.AsStream())
				{
					await stream.WriteAsync(heatmaps[(i % 7) + 1], 0, heatmaps[(i % 7) + 1].Length);
					var overlayMediaClip = await MediaClip.CreateFromImageFileAsync(await WriteableBitmapToStorageFile(wb, FileFormat.Tiff), new TimeSpan(0, 0, 0, 0, 100));
					mediaOverlay = new MediaOverlay(overlayMediaClip);
					mediaOverlay.Delay = new TimeSpan(0, 0, 0, 0, (i - 1) * 100);
					mediaOverlay.Position = overlayPosition;
					mediaOverlay.Opacity = 0.35;
					mediaOverlayLayer.Overlays.Add(mediaOverlay);

				}
				//videoLoading.Value = i * 100 / 200;
			}

			//videoLoading.Visibility = Visibility.Collapsed;

			//stopwatch.Stop();

			composition.OverlayLayers.Add(mediaOverlayLayer);

			//mediaPlayerElement.Source = MediaSource.CreateFromMediaStreamSource(mediaStreamSource);

			return MediaSource.CreateFromMediaStreamSource(mediaStreamSource);

		}

*/



		private static async Task<StorageFile> WriteableBitmapToStorageFile(WriteableBitmap WB, FileFormat fileFormat)
		{
			string FileName = "MyFile.";
			Guid BitmapEncoderGuid = BitmapEncoder.JpegEncoderId;
			switch (fileFormat)
			{
				case FileFormat.Jpeg:
					FileName += "jpeg";
					BitmapEncoderGuid = BitmapEncoder.JpegEncoderId; //7s
					break;
				case FileFormat.Tiff:
					FileName += "tiff";
					BitmapEncoderGuid = BitmapEncoder.TiffEncoderId; //7s
					break;
				case FileFormat.Gif:
					FileName += "gif";
					BitmapEncoderGuid = BitmapEncoder.GifEncoderId; //10s
					break;
				case FileFormat.Bmp:
					FileName += "bmp";
					BitmapEncoderGuid = BitmapEncoder.BmpEncoderId;
					break;
			}
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
		public enum FileFormat
		{
			Jpeg,
			Tiff,
			Gif,
			Bmp
		}
	}

}

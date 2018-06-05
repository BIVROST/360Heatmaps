﻿using Newtonsoft.Json;
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


        private static async Task<MediaOverlay> Test(List<Heatmap.Coord> coords, Rect overlayPosition, ColorPicker colorPicker, double heatmapOpacity)
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

            var clip = await MediaClip.CreateFromImageFileAsync(await WriteableBitmapToStorageFile(wb, FileFormat.Tiff), new TimeSpan(0, 0, 0, 0, 100));

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

        public static async Task<MediaOverlayLayer> GenerateVideoFromHeatmap(SessionCollection sessions, Rect overlayPosition, ColorPicker colorPicker, ProgressBar videoGeneratingProgress)
        {

            List<MediaStreamSource> mediaStreamSources = new List<MediaStreamSource>();
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

            videoGeneratingProgress.Maximum = min_length;
            videoGeneratingProgress.Visibility = Windows.UI.Xaml.Visibility.Visible;

            List<Heatmap.Coord>[] generatedCoords = new List<Heatmap.Coord>[min_length];
            for (int i = 0; i < min_length; i++)
            {
                generatedCoords[i] = new List<Heatmap.Coord>();
            }

            try
            {
                for (int i = 0; i < min_length; i++)
                {
                    for (int j = 0; j < sessions.sessions.Count; j++)
                    {
                        generatedCoords[i].Add(coordsArray[j][i]);
                        //Debug.WriteLine("j: " + j);
                    }
					//Debug.WriteLine("i: " + i);
					//mediaStreamSources.Add(await Test(generatedCoords[i], overlayPosition, colorPicker, 0.8));
					mediaOverlays.Add(await Test(generatedCoords[i], overlayPosition, colorPicker, 0.35));

					videoGeneratingProgress.Value = i;
                }
            }
            catch (Exception e)
            {

            }

            videoGeneratingProgress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

			int delay = 0;
			MediaOverlayLayer mediaOverlayLayer = new MediaOverlayLayer();
			foreach (MediaOverlay x in mediaOverlays)
			{
				x.Delay = new TimeSpan(0, 0, 0, 0, delay);
				delay += 100;
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

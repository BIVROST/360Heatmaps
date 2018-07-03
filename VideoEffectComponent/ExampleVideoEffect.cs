using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Foundation.Collections;
using Windows.Graphics.DirectX.Direct3D11;
using System.Runtime.InteropServices;
using Microsoft.Graphics.Canvas.Effects;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using System.Diagnostics;


namespace VideoEffectComponent
{

	[ComImport]
	[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	unsafe interface IMemoryBufferByteAccess
	{
		void GetBuffer(out byte* buffer, out uint capacity);
	}
	public sealed class ExampleVideoEffect : IBasicVideoEffect
	{

		private Dictionary<int, WriteableBitmap> heatmaps = new Dictionary<int, WriteableBitmap>();
		
		public ExampleVideoEffect()
		{
			Debug.WriteLine("ExampleVideoEffect constructor");
		}

		public MediaMemoryTypes SupportedMemoryTypes { get { return MediaMemoryTypes.Gpu; } }
		public bool TimeIndependent { get { return false; } }
		public void Close(MediaEffectClosedReason reason)
		{
			// Dispose of effect resources
		}

		private int frameCount;
		public void DiscardQueuedFrames()
		{
			frameCount = 0;
		}

		public bool IsReadOnly { get { return false; } }

		private CanvasDevice canvasDevice;
		public void SetEncodingProperties(VideoEncodingProperties encodingProperties, IDirect3DDevice device)
		{
			canvasDevice = CanvasDevice.CreateFromDirect3D11Device(device);
		}

		public IReadOnlyList<VideoEncodingProperties> SupportedEncodingProperties
		{
			get
			{
				var encodingProperties = new VideoEncodingProperties();
				encodingProperties.Subtype = "ARGB32";
				return new List<VideoEncodingProperties>() { encodingProperties };

				// If the list is empty, the encoding type will be ARGB32.
				// return new List<VideoEncodingProperties>();
			}
		}

		private IPropertySet configuration;
		public void SetProperties(IPropertySet configuration)
		{
			this.configuration = configuration;
		}

		public double BlurAmount
		{
			get
			{
				object val;
				if (configuration != null && configuration.TryGetValue("Blur", out val))
				{
					return (double)val;
				}
				return 3;
			}
		}
		private static int counter = 2;

		public void ProcessFrame(ProcessVideoFrameContext context)
		{

			using (CanvasBitmap inputBitmap = CanvasBitmap.CreateFromDirect3D11Surface(canvasDevice, context.InputFrame.Direct3DSurface))
			using (CanvasRenderTarget renderTarget = CanvasRenderTarget.CreateFromDirect3D11Surface(canvasDevice, context.OutputFrame.Direct3DSurface))
			using (CanvasDrawingSession ds = renderTarget.CreateDrawingSession())
			{




				var gaussianBlurEffect = new GaussianBlurEffect
				{
					Source = inputBitmap,
					BlurAmount = (float)BlurAmount,
					Optimization = EffectOptimization.Speed
				};

				ds.DrawImage(gaussianBlurEffect);

				if (counter % 2 == 0)
				{
					ds.DrawCircle(200, 200, 20, Colors.Red);
					ds.FillCircle(200, 200, 20, Colors.Red);
				}
				else
				{
					ds.DrawCircle(1200, 700, 20, Colors.OrangeRed);
					ds.FillCircle(1200, 700, 20, Colors.OrangeRed);
				}

				double dur = context.InputFrame.Duration.Value.TotalMilliseconds;
				double rel = context.InputFrame.RelativeTime.Value.TotalMilliseconds;

				Debug.Write(dur + "ms ===> ");
				Debug.WriteLine(rel + "ms ");
				Debug.WriteLine(Math.Round(rel/dur) + " frame -> i:" + counter);
				//Debug.WriteLine(context.InputFrame.SystemRelativeTime.Value.TotalMilliseconds.ToString());


				counter++;

				if (counter == int.MaxValue)
				{
					counter = 0;
				}

				

			}
		}
	}
}

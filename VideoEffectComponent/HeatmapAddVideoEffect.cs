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
using System.Diagnostics;
using BivrostHeatmapViewer;
using Microsoft.Graphics.Canvas.Brushes;

namespace VideoEffectComponent
{

    [ComImport]
	[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	unsafe interface IMemoryBufferByteAccess
	{
		void GetBuffer(out byte* buffer, out uint capacity);
	}


	public sealed class HeatmapAddVideoEffect : IBasicVideoEffect
	{
		//public static IDictionary<string, List<int>> coords;
		private Color[] _colors = new Color[5] { Colors.Orange, Colors.Purple, Colors.Brown, Colors.LightGreen, Colors.DarkSalmon };


		public HeatmapAddVideoEffect()
		{
			Debug.WriteLine("ExampleVideoEffect constructor");
		}

		public MediaMemoryTypes SupportedMemoryTypes => MediaMemoryTypes.Gpu;
		public bool TimeIndependent => false;

		public void Close(MediaEffectClosedReason reason)
		{
			// Dispose of effect resources
		}

		//private int frameCount;
		public void DiscardQueuedFrames()
		{
			//frameCount = 0;
		}

		public bool IsReadOnly => false;

		private CanvasDevice _canvasDevice;
		public void SetEncodingProperties(VideoEncodingProperties encodingProperties, IDirect3DDevice device)
		{
			_canvasDevice = CanvasDevice.CreateFromDirect3D11Device(device);
		}

		public IReadOnlyList<VideoEncodingProperties> SupportedEncodingProperties
		{
			get
			{
				var encodingProperties = new VideoEncodingProperties {Subtype = "ARGB32"};
				return new List<VideoEncodingProperties>() { encodingProperties };

				// If the list is empty, the encoding type will be ARGB32.
				// return new List<VideoEncodingProperties>();
			}
		}

		private IPropertySet _configuration;
		public void SetProperties(IPropertySet configuration)
		{
			_configuration = configuration;

			object count;
			if (configuration.TryGetValue("count", out count))
			{
				this._count = (int)count;

				object pitch;
				configuration.TryGetValue("pitch", out pitch);
				this._pitch = pitch as List<int>;

				object yaw;
				configuration.TryGetValue("yaw", out yaw);
				this._yaw = yaw as List<int>;

				object fov;
				configuration.TryGetValue("fov", out fov);
				this._fov = fov as List<int>;

				object offset;
				configuration.TryGetValue("offset", out offset);
				this._offset = (long)offset;
				

				object frameLength;
				configuration.TryGetValue("frameLength", out frameLength);
				this._frameLength = (double)frameLength;

				object width;
				configuration.TryGetValue("width", out width);
				this._width = (uint)width;

				object height;
				configuration.TryGetValue("height", out height);
				this._height = (uint)height;

				object generateDots;
				configuration.TryGetValue("generateDots", out generateDots);
				this._generateDots = (bool)generateDots;

				object dotsRadius;
				configuration.TryGetValue("dotsRadius", out dotsRadius);
				this._dotsRadius = (float)dotsRadius;

				object backgroundColor;
				configuration.TryGetValue("backgroundColor", out backgroundColor);
				this._backgroundColor = (Color) backgroundColor;

				object backgroundOpacity;
				configuration.TryGetValue("backgroundOpacity", out backgroundOpacity);
				this._backgroundOpacity = (float)backgroundOpacity;

				object heatmapOpacity;
				configuration.TryGetValue("heatmapOpacity", out heatmapOpacity);
				this._heatmapOpacity = (float)heatmapOpacity;

				object graysclaleVideoFlag;
				configuration.TryGetValue("grayscaleVideoFlag", out graysclaleVideoFlag);
				this._graysclaleVideoFlag = (bool)graysclaleVideoFlag;

				this._offset = (long)((double)(this._offset / TimeSpan.TicksPerMillisecond) / this._frameLength);
			}
		}

		private List<int> _pitch;
		private List<int> _yaw;
		private List<int> _fov;
		private int _count;
		private long _offset;
		private double _frameLength;
		private uint _width;
		private uint _height;
		private bool _generateDots;
		private float _dotsRadius;
		private Color _backgroundColor;
		private float _backgroundOpacity;
		private float _heatmapOpacity;
		private bool _graysclaleVideoFlag;
		//private bool correctionFlag = false;

		public void ProcessFrame(ProcessVideoFrameContext context)
		{


			using (CanvasBitmap inputBitmap = CanvasBitmap.CreateFromDirect3D11Surface(_canvasDevice, context.InputFrame.Direct3DSurface))
			using (CanvasRenderTarget renderTarget = CanvasRenderTarget.CreateFromDirect3D11Surface(_canvasDevice, context.OutputFrame.Direct3DSurface))
			using (CanvasDrawingSession ds = renderTarget.CreateDrawingSession())
			using (var scaleEffect = new ScaleEffect())
			using (CanvasSolidColorBrush solidColorBrush = new CanvasSolidColorBrush(_canvasDevice, _backgroundColor))
			{
				solidColorBrush.Opacity = _backgroundOpacity;
				double rel = context.InputFrame.RelativeTime.Value.Ticks/ (double)TimeSpan.TicksPerMillisecond;

				//context.OutputFrame.Duration = new TimeSpan( (long)(frameLength * TimeSpan.TicksPerMillisecond));

				
				

				int frameTimeCounter = (int)Math.Round(rel / _frameLength, 0);

                int[] pitch = new int[_count];
                int[] yaw = new int[_count];
                int[] fov = new int[_count];

                for (int i = 0; i < _count; i++)
                {
                    try
                    {
						//pitch[i] = this.pitch[ (frameTimeCounter + (int)Math.Round(offset, 0)) * (count) + i];
                        //fov[i] = this.fov[ (frameTimeCounter + (int)Math.Round(offset, 0)) * (count) + i];
						//yaw[i] = this.yaw[ (frameTimeCounter + (int)Math.Round(offset, 0)) * (count) + i];

						pitch[i] = this._pitch[(frameTimeCounter + (int)_offset) * (_count) + i];
						fov[i] = this._fov[(frameTimeCounter + (int)_offset) * (_count) + i];
						yaw[i] = this._yaw[(frameTimeCounter + (int)_offset) * (_count) + i];
					}
                    catch (ArgumentOutOfRangeException ex)
                    {
                        Debug.WriteLine(ex.Message);
                        pitch[i] = 0;
                        fov[i] = 0;
                        yaw[i] = 0;
                    }
                }

				byte[] tab = Heatmap.GenerateHeatmap(pitch, yaw, fov);
				CanvasBitmap cb = CanvasBitmap.CreateFromBytes(_canvasDevice, tab, 64, 64, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 96, CanvasAlphaMode.Premultiplied);
				scaleEffect.Source = cb;
				scaleEffect.Scale = new System.Numerics.Vector2( (float)_width / 64, (float)_height / 64);
				scaleEffect.InterpolationMode = CanvasImageInterpolation.Cubic;
				scaleEffect.BorderMode = EffectBorderMode.Hard;


				if (_graysclaleVideoFlag)
				{
					var grayScaleEffect = new GrayscaleEffect
					{
						BufferPrecision = CanvasBufferPrecision.Precision8UIntNormalized,
						CacheOutput = false,
						Source = inputBitmap
					};
					ds.DrawImage(grayScaleEffect);
				}
				else
				{
					ds.DrawImage(inputBitmap);
				}

				ds.DrawImage(scaleEffect, 0, 0, new Windows.Foundation.Rect { Height = _height, Width = _width }, _heatmapOpacity);



				if (_generateDots)
				{
					for (int i = 0; i < _count; i++)
					{
						ds.FillCircle(yaw[i] * _width / 64, pitch[i] * _height / 64, _dotsRadius, _colors[i % 5]);
					}
				}



				ds.FillRectangle(new Windows.Foundation.Rect { Height = _height, Width = _width }, solidColorBrush);

				ds.Flush();
			}

		}
	}

}

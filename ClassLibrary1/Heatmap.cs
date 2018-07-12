using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;

namespace BivrostHeatmapViewer
{
	public class HeatmapComparer : EqualityComparer<Heatmap.Coord>
	{
		public override bool Equals(Heatmap.Coord x, Heatmap.Coord y)
		{
			if (x.fov == y.fov && x.pitch == y.pitch && x.yaw == y.yaw)
			{
				return true;
			}

			return false;
		}

		public override int GetHashCode(Heatmap.Coord obj)
		{
			int hCode = obj.yaw ^ obj.pitch ^ obj.fov;
			return hCode.GetHashCode();
		}
	}

	public class Heatmap
	{
		public class Coord
		{
			public int yaw;
			public int pitch;
			public int fov;
		}

		static List<char> base64 = new List<char>("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=".ToCharArray());

		public static List<Coord> CoordsDeserialize(string serialized)
		{
			var coords = new List<Coord>();
			int fov = 0;
			int i = 0;

			while (serialized.Length > i)
			{
				if (serialized[i] == '!')
				{   // event
					string ev = "";
					for (i = i + 1; serialized[i] != '!'; i++)
					{
						ev += serialized[i];
					}
					var event_code = ev[0];
					var event_body = ev.Substring(1);
					i++;
					switch (event_code)
					{
						case 'F':
							fov = int.Parse(event_body);
							break;
						default:
							System.Diagnostics.Debug.WriteLine("unknown event: " + event_code + event_body);
							break;
					}
				}
				else
				{
					var yaw = base64.IndexOf(serialized[i]);
					var pitch = base64.IndexOf(serialized[i + 1]);
					i += 2;
					if (yaw >= 0 && pitch >= 0) // ignore unknown parts ("--") and broken messages
						coords.Add(new Coord() { yaw = yaw, pitch = pitch, fov = fov });
					else
					{
						coords.Add(new Coord() { yaw = 0, pitch = 0, fov = 0 });
					}
				}
			}

			return coords;
		}

		public static float distance(float lon1, float lat1, float lon2, float lat2)
		{
			var φ1 = (lat1 - 0.5) * Math.PI;
			var φ2 = (lat2 - 0.5) * Math.PI;
			var Δλ = (lon2 - lon1) * Math.PI * 2;
			//var R = 6371000; // gives d in metres
			var d = Math.Acos(Math.Sin(φ1) * Math.Sin(φ2) + Math.Cos(φ1) * Math.Cos(φ2) * Math.Cos(Δλ));
			return (float)d;
		}

		//public static Func<float,float,float,float,float>



		static float fovConverter(float fov) { return fov; }

		static float[] distanceOptCache = null;

		/**
		 * Optimized version of haversine. All arguments must be in range of 0..63 and lon2 is unused.
		 * @type Function(number, number, number, number):number
		 */
		static protected float distanceOpt(int lon1_, int lat1_, int lon2_, int lat2_)
		{
			if (distanceOptCache == null)
			{
				distanceOptCache = new float[64 * 64 * 64];
				for (var lon1 = 0; lon1 < 64; lon1++)
				{
					for (var lat1 = 0; lat1 < 64; lat1++)
					{
						for (var lat2 = 0; lat2 < 64; lat2++)
						{
							distanceOptCache[lon1 * 64 * 64 + lat1 * 64 + lat2] = distance(lon1 / 63f, lat1 / 63f, 0, lat2 / 63f);
						}
					}
				};
			}

			return distanceOptCache[lon1_ * 64 * 64 + lat1_ * 64 + lat2_];
		}




		/**
		 * Generates heatmaps from coordinates
		 * @param {Array<Array<number>>} coords look coordinates, index 0 is yaw, index 1 is pitch, index 2 is fov
		 * @param {?function(number):number} fovConverter convertion function for FOV values
		 * @returns {Array<Array<Array<number>>>}
		 */
		//public static float[,] Generate(List<Coord> coords)
		public static float[] Generate(List<Coord> coords)
		{
			// fuzziness of edges, higher => less
			var fuzziness = 4;

			// heatmap initialization
			//var hm = new Array(64);
			//var hm = new float[64, 64];
			var hm = new float[64 * 64];
			for (var y = 0; y < 64; y++)
			{
				//				var row = hm[y] = new Array(64);
				for (var x = 0; x < 64; x++)
					//hm[y,x] = 0;
					hm[y * 64 + x] = 0;
			}

			// generate fov tables	
			//var fov_tables = new Array(64);
			var fov_tables = new int[64 * 64 * 64];
			//for (var yy = 0; yy < 64; yy++)
			//{
			//	//var table = fov_tables[yy] = new Array(64);
			//	//for (var y = 0; y < 64; y++)
			//		//table[y] = new Uint16Array(64);
			//		//fov_tables[yy, y] = 0;
			//}


			// generate heatmap
			int fov = -1;
			for (var i = 0; i < coords.Count; i++)
			{
				var p = coords[i];

				if (p == null)
					continue;

				// regenerate fov tables
				if (p.fov != fov)
				{
					fov = p.fov;
					var halfFovRadians = Math.PI * fovConverter(fov) / 360f; // half fov radians
					for (var yy = 0; yy < 64; yy++)
					{
						//var table = fov_tables[yy];
						for (var y = 0; y < 64; y++)
						{
							//var row = fov_tables[yy,y];
							for (var x = 0; x < 64; x++)
							{
								// distanceOpt(x, y, 0, yy); //
								var v = halfFovRadians - distanceOpt(x, y, 0, yy); //distance(x/63f,y/63f,0,yy/63f);
								v *= 256 * fuzziness;
								if (v < 1)
									v = 0;
								if (v > 255)    // 256 levels of fuzziness
									v = 255;
								fov_tables[yy * 64 * 64 + y * 64 + x] = (int)v;
							}
						}
					}
				}

				var ft_y = p.pitch;
				for (var y = 0; y < 64; y++)
				{
					for (var x = 0; x < 64; x++)
					{
						var ft_x = (128 + p.yaw - x) % 64;
						//hm[y,x] += fov_tables[ft_y,y,ft_x];
						//hm[x * 64 + y] += fov_tables[ft_y * 64 * 64 + y * 64 + ft_x];
						hm[y * 64 + x] += fov_tables[ft_y * 64 * 64 + y * 64 + ft_x];
					}
				}
			}

			////	// unoptimized
			//for (int ii = 0; ii < coords.Count; ii++)
			//{
			//	var p = coords[ii];
			//	for (var y = 0; y < 64; y++)
			//	{
			//		for (var x = 0; x < 64; x++)
			//		{
			//			if (distance(x / 63f, y / 63f, p.yaw, p.pitch) < fov)
			//				hm[x * 64 + y]++;
			//		}
			//	}
			//}

			// heatmap normalization:
			// find range
			float hm_min = float.PositiveInfinity;
			float hm_max = 0;
			for (var y = 0; y < 64; y++)
			{
				//var row = hm[y];
				for (var x = 0; x < 64; x++)
				{
					//var cell = hm[y,x];
					var cell = hm[y * 64 + x];
					if (cell < hm_min) hm_min = cell;
					if (cell > hm_max) hm_max = cell;
				}
			}
			// provide sane values on flat heatmap
			if (hm_min == hm_max)
			{
				hm_min = 0;
				hm_max = 1;
			}
			// normalize
			var rev_hm_max_minus_hm_min = 1 / (hm_max - hm_min);
			for (var y = 0; y < 64; y++)
			{
				//var row = hm[y];
				for (var x = 0; x < 64; x++)
				{
					//hm[y,x] = (hm[y,x] - hm_min) * rev_hm_max_minus_hm_min;
					hm[y * 64 + x] = (hm[y * 64 + x] - hm_min) * rev_hm_max_minus_hm_min;
				}
			}
			return hm;
		}

		public static byte[] RenderHeatmap(float[,] heatmap)
		{
			byte[] image = new byte[64 * 64 * 4];

			for (int y = 0; y < 64; y++)
				for (int x = 0; x < 64; x++)
				{
					var c = Microsoft.Toolkit.Uwp.Helpers.ColorHelper.FromHsl(240 * (1 - heatmap[y, x]), 1, 0.5);


					image[(y * 64 + x) * 4 + 0] = c.B;
					image[(y * 64 + x) * 4 + 1] = c.G;
					image[(y * 64 + x) * 4 + 2] = c.R;
					image[(y * 64 + x) * 4 + 3] = 255;
				}

			return image;
		}

		public static byte[] RenderHeatmap(float[] heatmap)
		{
			byte[] image = new byte[64 * 64 * 4];

			for (int it = 0; it < heatmap.Length; it++)
			{
				var c = Microsoft.Toolkit.Uwp.Helpers.ColorHelper.FromHsl(240 * (1 - heatmap[it]), 1, 0.5);
				image[it * 4 + 0] = c.B;
				image[it * 4 + 1] = c.G;
				image[it * 4 + 2] = c.R;
				image[it * 4 + 3] = 255;
			}

			return image;
		}

		public static byte[] GenerateHeatmap(int[] pitch, int[] yaw, int[] fov)
		{
            List<Coord> coords = new List<Coord>();

            for (int i = 0; i < pitch.Length; i++)
            {
                coords.Add(
                    new Coord
                    {
                        pitch = pitch[i],
                        yaw = yaw[i],
                        fov = fov[i]
                    }
                    );
            }

			var heatmap = Generate(coords);
			var renderedHeatmap = RenderHeatmap(heatmap);

			//CanvasBitmap cb = CanvasBitmap.CreateFromBytes(CanvasDevice.GetSharedDevice(), renderedHeatmap, 64, 64, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
						
			return renderedHeatmap;
		}
	}
}

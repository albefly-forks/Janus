using System.Collections.Generic;
using System.Drawing;

namespace WeifenLuo.WinFormsUI.Docking
{
	/// <summary>
	/// Картинки докинга для разных DPI. Для каждой картинки в ресурсах есть
	/// заранее подготовленные варианты 150% и 200% (суффиксы _150 и _200).
	/// Картинки не масштабируются на лету: часть из них сравнивается попиксельно
	/// (регион ромба, карта активных зон), поэтому берется ближайший набор.
	/// </summary>
	internal static class DpiResources
	{
		private static readonly Dictionary<string, Bitmap> _bitmaps = new Dictionary<string, Bitmap>();
		private static readonly Dictionary<Bitmap, string> _names = new Dictionary<Bitmap, string>();
		private static readonly object _lock = new object();

		/// <summary>
		/// Масштаб (в процентах) набора картинок, ближайшего к заданному DPI.
		/// </summary>
		public static int GetScalePercent(int dpi)
		{
			var percent = dpi * 100 / 96;
			return percent < 125 ? 100 : percent < 175 ? 150 : 200;
		}

		/// <summary>
		/// Картинка из ресурсов докинга для заданного DPI.
		/// </summary>
		public static Bitmap Get(string name, int dpi = 96)
		{
			var percent = GetScalePercent(dpi);
			var key = percent == 100 ? name : name + "_" + percent;
			lock (_lock)
			{
				Bitmap bitmap;
				if (_bitmaps.TryGetValue(key, out bitmap))
					return bitmap;

				bitmap = Resources.ResourceManager.GetObject(key, Resources.Culture) as Bitmap;
				if (bitmap == null)
				{
					if (percent == 100)
						return null;
					// Варианта для этого масштаба нет
					bitmap = Get(name);
				}
				else
					_names[bitmap] = name;

				_bitmaps.Add(key, bitmap);
				return bitmap;
			}
		}

		/// <summary>
		/// Вариант картинки, полученной через <see cref="Get"/>, для другого DPI.
		/// </summary>
		public static Bitmap ForDpi(Bitmap image, int dpi)
		{
			if (image == null)
				return null;
			string name;
			lock (_lock)
				if (!_names.TryGetValue(image, out name))
					return image;
			return Get(name, dpi);
		}
	}
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Resources;

using CodeJam.Extensibility;

using Rsdn.Janus.Framework.Imaging;

namespace Rsdn.Janus
{
	/// <summary>
	/// Обеспечивает работу со сменяемыми картинками.
	/// </summary>
	[Service(typeof (IStyleImageManager))]
	internal class StyleImageManager : IStyleImageManager, IDisposable
	{
		private const string _sizeGroup = "size";
		private const string _nameGroup = "name";
		private const string _imagesResource = ApplicationInfo.ResourcesNamespace + "Images.resources";
		private const string _defaultTheme = "_default";

		private static readonly Regex _imageNameRx = new Regex(
			@"(?:(?'" + _nameGroup + @"'.+)(?'" + _sizeGroup + @"'16|24)\Z)|" +
			@"(?'" + _nameGroup + @"'.+)\Z",
			RegexOptions.Compiled);

		private readonly Dictionary<string, StyledImage> _constSizeImages =
			new Dictionary<string, StyledImage>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, StyledImage> _smallSizeImages =
			new Dictionary<string, StyledImage>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, StyledImage> _largeSizeImages =
			new Dictionary<string, StyledImage>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, Image> _images =
			new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, string> _imageNamesWithExt =
			new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		// Заранее подготовленные наборы картинок для высокого DPI (масштаб в процентах).
		private static readonly int[] _scaledSetPercents = { 150, 200 };
		private readonly Dictionary<int, Dictionary<string, Image>> _scaledSets =
			new Dictionary<int, Dictionary<string, Image>>();
		private readonly Dictionary<string, Image> _scaledImages =
			new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
		// Картинки, замененные внешними, - для них заготовленные наборы неактуальны.
		private readonly HashSet<string> _externalImages =
			new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private readonly object _scaledLock = new object();

		public StyleImageManager()
		{
			var imagesResource = GetImagesResource();
			using (var rr = new ResourceReader(imagesResource))
			{
				LoadImages(rr, StyleConfig.Instance.Theme + Path.DirectorySeparatorChar, false);
				if (StyleConfig.Instance.Theme != _defaultTheme)
					LoadImages(rr, _defaultTheme + Path.DirectorySeparatorChar, false);
			}
		}

		private static Stream GetImagesResource()
		{
			var imagesResource =
				EnvironmentHelper.JanusAssembly.GetManifestResourceStream(_imagesResource);
			if (imagesResource == null)
				throw new ApplicationException("Could not load style images resource");
			return imagesResource;
		}

		private static string GetScaledImagesResourceName(int percent)
		{
			return ApplicationInfo.ResourcesNamespace + "Images" + percent + ".resources";
		}

		public void LoadExternalImages(string fileName)
		{
			if (fileName.ToLower().Contains(".resx"))
				using (var rr = new ResXResourceReader(fileName))
					LoadImages(rr, string.Empty, true);
			else
				using (var rr = new ResourceReader(fileName))
					LoadImages(rr, string.Empty, true);
		}

		public static IEnumerable<string> CollectStyleDirs()
		{
			var dirs = new Dictionary<string, bool>();
			using (var rr = new ResourceReader(GetImagesResource()))
				foreach (DictionaryEntry e in rr)
				{
					var key = e.Key.ToString();
					if (!(e.Value is Image))
						continue;

					var separatorPos = key.IndexOf(Path.DirectorySeparatorChar);
					if (separatorPos == -1)
						continue;

					var dirName = key.Substring(0, separatorPos);
					if (dirs.ContainsKey(dirName))
						continue;

					dirs.Add(dirName, true);
				}

			return dirs.Keys;
		}

		private void LoadImages(IResourceReader rr, string dir, bool replaceExisting)
		{
			foreach (DictionaryEntry e in rr)
			{
				var key = e.Key.ToString();
				if (dir.Length > 0 && !key.StartsWith(dir) || !(e.Value is Image))
					continue;

				var uri = dir.Length > 0 ? key.Substring(dir.Length) : key;
				var separatorPos = uri.IndexOf(Path.DirectorySeparatorChar);
				var namePrefix = separatorPos != -1
					? Path.GetDirectoryName(uri) + Path.DirectorySeparatorChar
					: string.Empty;

				if (!replaceExisting && _images.ContainsKey(uri))
					continue;

				LoadImage(uri, Path.GetFileName(uri), namePrefix);
				if (_images.ContainsKey(uri))
				{
					var img = _images[uri];

					var nameWithExt = $"{uri}.{ImageFormatInfo.FromImageFormat(img.RawFormat).Extension}";
					_imageNamesWithExt.Remove(nameWithExt);

					img.Dispose();
					_images.Remove(uri);
				}

				var newImage = (Image)e.Value;

				var newNameWithExt = $"{uri}.{ImageFormatInfo.FromImageFormat(newImage.RawFormat).Extension}";
				_imageNamesWithExt.Add(newNameWithExt, uri);

				_images.Add(uri, newImage);

				if (replaceExisting)
					lock (_scaledLock)
					{
						_externalImages.Add(uri);
						// Отмасштабированные копии могут использоваться контролами, поэтому не освобождаются
						_scaledImages.Clear();
					}
			}
		}

		private void LoadImage(string uri, string fileName, string namePrefix)
		{
			string cultureName = null;
			var imgName     = fileName;

			var pointPos = fileName.IndexOf('.');
			if (pointPos != -1)
			{
				cultureName = fileName.Substring(pointPos + 1);
				imgName     = fileName.Substring(0, pointPos);
			}

			var m = _imageNameRx.Match(imgName);
			if (!m.Success)
				return;

			var name = namePrefix + m.Groups[_nameGroup].Value;
			Dictionary<string, StyledImage> hash;
			switch (m.Groups[_sizeGroup].Value)
			{
				case "16":
					hash = _smallSizeImages;
					break;
				case "24":
					hash = _largeSizeImages;
					break;
				default:
					hash = _constSizeImages;
					break;
			}
			if (!hash.ContainsKey(name))
				hash.Add(name, new StyledImage());
			hash[name].SetUri(uri, cultureName);
		}

		private StyledImage GetStyleImage(string name, StyleImageType imageType)
		{
			Dictionary<string, StyledImage> hash;
			switch (imageType)
			{
				case StyleImageType.ConstSize:
					hash = _constSizeImages;
					break;
				case StyleImageType.Small:
					hash = _smallSizeImages;
					break;
				case StyleImageType.Large:
					hash = _largeSizeImages;
					break;
				case StyleImageType.Default:
					hash = Config.Instance.ToolbarImageSize == ToolbarImageSize.Size16
						? _smallSizeImages
						: _largeSizeImages;
					break;
				default:
					throw new ArgumentException("Unknown image type", nameof(imageType));
			}
			StyledImage image;
			if (!hash.TryGetValue(name, out image))
				return null;
			return image;
		}

		#region IStyleImageManager Members

		public Image GetImage(string name, StyleImageType imageType)
		{
			var si = GetStyleImage(name, imageType);
			return
				si == null
					? null
					: _images[si.GetUri(Thread.CurrentThread.CurrentUICulture)];
		}

		public Image GetImage(string name, StyleImageType imageType, int dpi)
		{
			var si = GetStyleImage(name, imageType);
			return
				si == null
					? null
					: GetScaledImage(si.GetUri(Thread.CurrentThread.CurrentUICulture), dpi);
		}

		public string GetImageUri(string name, StyleImageType imageType)
		{
			var si = GetStyleImage(name, imageType);
			return
				si == null
					? null
					: JanusProtocolInfo.FormatURI(JanusProtocolResourceType.Image,
							si.GetUri(Thread.CurrentThread.CurrentUICulture)
							.Replace(
								Path.DirectorySeparatorChar,
								JanusProtocolInfo.ProtocolSeparatorChar));
		}

		public Image GetImage(string uri)
		{
			var internalName = ResolveUri(uri);
			return internalName != null ? _images[internalName] : null;
		}

		public Image GetImage(string uri, int dpi)
		{
			var internalName = ResolveUri(uri);
			return internalName != null ? GetScaledImage(internalName, dpi) : null;
		}

		#endregion

		private string ResolveUri(string uri)
		{
			var internalName = uri.Replace(
				JanusProtocolInfo.ProtocolSeparatorChar,
				Path.DirectorySeparatorChar);

			if (_images.ContainsKey(internalName))
				return internalName;
			return
				_imageNamesWithExt.TryGetValue(internalName, out internalName)
					? internalName
					: null;
		}

		#region High DPI

		/// <summary>
		/// Получить картинку, размер которой в пикселях соответствует заданному DPI.
		/// Источником служит ближайший больший заготовленный набор (150% или 200%),
		/// при необходимости картинка доводится до точного размера.
		/// </summary>
		private Image GetScaledImage(string uri, int dpi)
		{
			Image baseImage;
			if (!_images.TryGetValue(uri, out baseImage))
				return null;
			if (dpi <= DpiHelper.LogicalDpi)
				return baseImage;

			lock (_scaledLock)
			{
				var key = uri + "|" + dpi;
				Image result;
				if (_scaledImages.TryGetValue(key, out result))
					return result;

				var targetSize = DpiHelper.Scale(baseImage.Size, dpi);
				var source = _externalImages.Contains(uri) ? null : FindScaledSource(uri, dpi);
				if (source == null)
					source = baseImage;

				// Анимацию при масштабировании не сохранить - отдаем заготовленный кадр как есть
				result =
					source.Size == targetSize || ImageAnimator.CanAnimate(source)
						? source
						: DpiHelper.ResizeImage(source, targetSize);

				_scaledImages.Add(key, result);
				return result;
			}
		}

		private Image FindScaledSource(string uri, int dpi)
		{
			Image found = null;
			foreach (var percent in _scaledSetPercents)
			{
				Image img;
				if (!GetScaledSet(percent).TryGetValue(uri, out img))
					continue;
				found = img;
				// Берем наименьший набор, который не придется увеличивать
				if (percent * DpiHelper.LogicalDpi >= dpi * 100)
					break;
			}
			return found;
		}

		private Dictionary<string, Image> GetScaledSet(int percent)
		{
			Dictionary<string, Image> set;
			if (_scaledSets.TryGetValue(percent, out set))
				return set;

			set = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
			_scaledSets.Add(percent, set);

			var stream =
				EnvironmentHelper.JanusAssembly.GetManifestResourceStream(GetScaledImagesResourceName(percent));
			if (stream == null)
				return set;

			using (var rr = new ResourceReader(stream))
			{
				LoadScaledSet(rr, set, StyleConfig.Instance.Theme + Path.DirectorySeparatorChar);
				if (StyleConfig.Instance.Theme != _defaultTheme)
					LoadScaledSet(rr, set, _defaultTheme + Path.DirectorySeparatorChar);
			}
			return set;
		}

		private static void LoadScaledSet(IResourceReader rr, IDictionary<string, Image> set, string dir)
		{
			foreach (DictionaryEntry e in rr)
			{
				var key = e.Key.ToString();
				var img = e.Value as Image;
				if (img == null || !key.StartsWith(dir))
					continue;

				var uri = key.Substring(dir.Length);
				if (set.ContainsKey(uri))
					img.Dispose();
				else
					set.Add(uri, img);
			}
		}

		#endregion

		#region IDisposable Members

		public void Dispose()
		{
			lock (_scaledLock)
			{
				var disposed = new HashSet<Image>(_images.Values);
				foreach (var image in _scaledSets.Values.SelectMany(set => set.Values).Concat(_scaledImages.Values))
					if (disposed.Add(image))
						image.Dispose();
				_scaledSets.Clear();
				_scaledImages.Clear();
			}

			foreach (var image in _images.Values)
				image.Dispose();
			_images.Clear();
		}

		#endregion

		#region StyleImage class
		private class StyledImage
		{
			private string _path;
			private Dictionary<string, string> _cultureDependentPath;

			public string GetUri(CultureInfo culture)
			{
				if (_cultureDependentPath != null)
				{
					string path;
					if (_cultureDependentPath.TryGetValue(culture.Name, out path))
						return path;
					if (culture.Parent != null && _cultureDependentPath.TryGetValue(culture.Parent.Name, out path))
						return path;
				}
				return _path;
			}

			public void SetUri(string path, string cultureName)
			{
				if (string.IsNullOrEmpty(cultureName))
				{
					if (string.IsNullOrEmpty(_path))
						_path = path;
				}
				else
				{
					if (_cultureDependentPath == null)
						_cultureDependentPath = new Dictionary<string, string>();
					else if (_cultureDependentPath.ContainsKey(cultureName))
						return;
					_cultureDependentPath.Add(cultureName, path);
				}
			}
		}
		#endregion
	}
}

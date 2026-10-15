using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using JetBrains.Annotations;

namespace Rsdn.Janus
{
	/// <summary>
	/// <see cref="ImageList"/>, картинки которого соответствуют DPI монитора контрола-владельца.
	/// При смене DPI список перестраивается с сохранением индексов картинок.
	/// </summary>
	public sealed class DpiImageList : IDisposable
	{
		private readonly List<Func<int, Image>> _factories = new List<Func<int, Image>>();
		private readonly Size _logicalSize;
		private IDisposable _dpiSubscription;
		private int _dpi;

		public DpiImageList() : this(new Size(16, 16)) {}

		public DpiImageList(Size logicalSize)
		{
			_logicalSize = logicalSize;
			_dpi = DpiHelper.SystemDpi;
			ImageList = new ImageList
			{
				ColorDepth = ColorDepth.Depth32Bit,
				ImageSize = DpiHelper.Scale(logicalSize, _dpi)
			};
		}

		/// <summary>
		/// Список картинок для назначения контролам.
		/// </summary>
		[NotNull]
		public ImageList ImageList { get; }

		/// <summary>
		/// DPI, для которого построен список.
		/// </summary>
		public int Dpi
		{
			get { return _dpi; }
			set
			{
				if (_dpi == value)
					return;
				_dpi = value;
				Rebuild();
			}
		}

		/// <summary>
		/// Привязать DPI списка к DPI контрола.
		/// </summary>
		public DpiImageList AttachTo([NotNull] Control owner)
		{
			if (owner == null)
				throw new ArgumentNullException(nameof(owner));

			_dpiSubscription?.Dispose();
			_dpiSubscription = DpiHelper.SubscribeDpiChanged(owner, dpi => Dpi = dpi);
			Dpi = owner.DeviceDpi;
			return this;
		}

		/// <summary>
		/// Добавить картинку, получаемую для конкретного DPI.
		/// </summary>
		/// <returns>Индекс картинки или -1, если картинки нет.</returns>
		public int Add([NotNull] Func<int, Image> imageFactory)
		{
			if (imageFactory == null)
				throw new ArgumentNullException(nameof(imageFactory));

			var image = imageFactory(_dpi);
			if (image == null)
				return -1;
			_factories.Add(imageFactory);
			ImageList.Images.Add(image);
			return ImageList.Images.Count - 1;
		}

		/// <summary>
		/// Добавить картинку, которую источник отдает для DPI из <see cref="ImageDpiScope"/>.
		/// </summary>
		/// <returns>Индекс картинки или -1, если картинки нет.</returns>
		public int AddScoped([NotNull] Func<Image> imageFactory)
		{
			if (imageFactory == null)
				throw new ArgumentNullException(nameof(imageFactory));

			return Add(
				dpi =>
				{
					using (ImageDpiScope.Enter(dpi))
						return imageFactory();
				});
		}

		/// <summary>
		/// Добавить картинку стиля.
		/// </summary>
		/// <returns>Индекс картинки или -1, если картинки нет.</returns>
		public int Add(
			[NotNull] IStyleImageManager styleImageManager,
			[NotNull] string name,
			StyleImageType imageType)
		{
			if (styleImageManager == null)
				throw new ArgumentNullException(nameof(styleImageManager));

			return Add(dpi => styleImageManager.GetImage(name, imageType, dpi));
		}

		private void Rebuild()
		{
			var images = new Image[_factories.Count];
			for (var i = 0; i < images.Length; i++)
				images[i] = _factories[i](_dpi) ?? new Bitmap(1, 1);

			ImageList.Images.Clear();
			ImageList.ImageSize = DpiHelper.Scale(_logicalSize, _dpi);
			ImageList.Images.AddRange(images);
		}

		public void Dispose()
		{
			_dpiSubscription?.Dispose();
			_dpiSubscription = null;
			ImageList.Dispose();
		}
	}
}

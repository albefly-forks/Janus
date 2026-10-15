using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Drawing;

using JetBrains.Annotations;

namespace Rsdn.Janus
{
	/// <summary>
	/// Расширения для <see cref="IStyleImageManager"/>.
	/// </summary>
	public static class StyleImageManagerHelper
	{
		/// <summary>
		/// Добавить картинку с указанным типом в <see cref="ImageList"/>.
		/// </summary>
		/// <returns>Индекс добавлененой картинки или -1 если картинка не найдена.</returns>
		public static int AppendImage(
			this IStyleImageManager styleImageManager,
			[Localizable(false)]string name,
			StyleImageType imageType,
			ImageList imageList)
		{
			if (styleImageManager == null)
				throw new ArgumentNullException("styleImageManager");

			var img = styleImageManager.GetImage(name, imageType);
			if (img == null)
				return -1;
			imageList.Images.Add(img);
			return imageList.Images.Count - 1;
		}

		/// <summary>
		/// Попытаться получить картику, в случае если картинка найдена не будет
		/// вернуть специальную картинку, показывающую, что искомая картинка не найдена.
		/// </summary>
		public static Image TryGetImage(
			[NotNull] this IStyleImageManager styleImageManager,
			[NotNull] string name,
			StyleImageType imageType)
		{
			if (styleImageManager == null)
				throw new ArgumentNullException("styleImageManager");
			if (name == null)
				throw new ArgumentNullException("name");

			return styleImageManager.GetImage(name, imageType);
			//ToDo: сппециальная картинка для отсутствующих картинок
		}

		/// <summary>
		/// Назначить картинку стиля контролу с учетом DPI его монитора
		/// и переназначать ее при смене DPI.
		/// </summary>
		/// <param name="styleImageManager">Менеджер картинок.</param>
		/// <param name="owner">Контрол, по DPI которого выбирается картинка.</param>
		/// <param name="name">Имя картинки.</param>
		/// <param name="imageType">Тип картинки.</param>
		/// <param name="apply">Назначение картинки.</param>
		/// <returns>Объект, отменяющий отслеживание DPI.</returns>
		public static IDisposable BindImage(
			[NotNull] this IStyleImageManager styleImageManager,
			[NotNull] Control owner,
			[NotNull] string name,
			StyleImageType imageType,
			[NotNull] Action<Image> apply)
		{
			if (styleImageManager == null)
				throw new ArgumentNullException(nameof(styleImageManager));
			if (owner == null)
				throw new ArgumentNullException(nameof(owner));
			if (apply == null)
				throw new ArgumentNullException(nameof(apply));

			apply(styleImageManager.GetImage(name, imageType, owner.DeviceDpi));
			return DpiHelper.SubscribeDpiChanged(
				owner,
				dpi => apply(styleImageManager.GetImage(name, imageType, dpi)));
		}

		/// <summary>
		/// Получить иконку окна, содержащую картинку стиля во всех размерах,
		/// которые Windows запрашивает при масштабах от 100% до 400%.
		/// </summary>
		/// <returns>Иконка или null, если картинка не найдена.</returns>
		public static Icon GetIcon(
			[NotNull] this IStyleImageManager styleImageManager,
			[NotNull] string name,
			StyleImageType imageType)
		{
			if (styleImageManager == null)
				throw new ArgumentNullException(nameof(styleImageManager));

			var baseImage = styleImageManager.GetImage(name, imageType);
			if (baseImage == null)
				return null;

			// Маленькая иконка - 16 логических пикселей, большая - 32
			var sizes = new[] { 16, 20, 24, 32, 40, 48, 64 };
			var frames = new Image[sizes.Length];
			for (var i = 0; i < sizes.Length; i++)
				frames[i] = styleImageManager.GetImage(
					name,
					imageType,
					sizes[i] * DpiHelper.LogicalDpi / baseImage.Width);
			return IconBuilder.Create(frames);
		}

		/// <summary>
		/// Попытаться получить картинку для заданного DPI.
		/// </summary>
		public static Image TryGetImage(
			[NotNull] this IStyleImageManager styleImageManager,
			[NotNull] string name,
			StyleImageType imageType,
			int dpi)
		{
			if (styleImageManager == null)
				throw new ArgumentNullException(nameof(styleImageManager));
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return styleImageManager.GetImage(name, imageType, dpi);
		}
	}
}
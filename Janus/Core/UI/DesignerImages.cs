using System;
using System.Collections.Generic;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

using JetBrains.Annotations;

namespace Rsdn.Janus
{
	/// <summary>
	/// Подменяет картинки, заданные в дизайнере форм, вариантами для высокого DPI.
	/// Варианты лежат в Resources\DesignerImages.resx с ключами
	/// <c>&lt;Форма&gt;.&lt;контрол&gt;_150</c> и <c>_200</c>
	/// (генерируются Graphics\HiDpi\generate_hidpi.py) - в ресурсах самих форм их хранить нельзя,
	/// дизайнер удаляет оттуда неизвестные ему записи.
	/// </summary>
	public static class DesignerImages
	{
		private static readonly ResourceManager _resources = new ResourceManager(
			ApplicationInfo.ResourcesNamespace + "DesignerImages",
			typeof(DesignerImages).Assembly);

		/// <summary>
		/// Применить картинки для DPI формы и обновлять их при смене DPI.
		/// </summary>
		public static void Apply([NotNull] Control form)
		{
			if (form == null)
				throw new ArgumentNullException(nameof(form));

			var targets = new List<ImageTarget>();
			CollectTargets(form, form.GetType().Name, targets);
			if (targets.Count == 0)
				return;

			Action<int> apply =
				dpi =>
				{
					foreach (var target in targets)
						target.Apply(dpi);
				};
			apply(form.DeviceDpi);
			DpiHelper.SubscribeDpiChanged(form, apply);
		}

		private static void CollectTargets(Control control, string formName, ICollection<ImageTarget> targets)
		{
			foreach (Control child in control.Controls)
			{
				var target = ImageTarget.TryCreate(child, formName);
				if (target != null)
					targets.Add(target);
				CollectTargets(child, formName, targets);
			}
		}

		private static Image GetVariant(string key)
		{
			try
			{
				return _resources.GetObject(key) as Image;
			}
			catch (MissingManifestResourceException)
			{
				return null;
			}
		}

		/// <summary>
		/// Контрол с картинкой из дизайнера и ее вариантами.
		/// </summary>
		private sealed class ImageTarget
		{
			private readonly Image _original;
			private readonly string _key;
			private readonly Action<Image> _setImage;

			private ImageTarget(Image original, string key, Action<Image> setImage)
			{
				_original = original;
				_key = key;
				_setImage = setImage;
			}

			public static ImageTarget TryCreate(Control control, string formName)
			{
				if (string.IsNullOrEmpty(control.Name))
					return null;

				Image image;
				Action<Image> setImage;
				var button = control as ButtonBase;
				var label = control as Label;
				var pictureBox = control as PictureBox;
				if (button != null)
				{
					image = button.Image;
					setImage = img => button.Image = img;
				}
				else if (label != null)
				{
					image = label.Image;
					setImage = img => label.Image = img;
				}
				else if (pictureBox != null)
				{
					image = pictureBox.Image;
					setImage = img => pictureBox.Image = img;
				}
				else
					return null;

				var key = formName + "." + control.Name;
				// Контролы без заготовленных вариантов не трогаем
				if (image == null || GetVariant(key + "_200") == null)
					return null;
				return new ImageTarget(image, key, setImage);
			}

			public void Apply(int dpi)
			{
				Image image;
				if (!_images.TryGetValue(dpi, out image))
				{
					var percent = dpi * 100 / DpiHelper.LogicalDpi;
					// Берем ближайший заготовленный вариант не меньше нужного и доводим до точного размера
					var source =
						percent <= 100
							? _original
							: GetVariant(_key + (percent <= 150 ? "_150" : "_200")) ?? _original;
					var size = DpiHelper.Scale(_original.Size, dpi);
					image = source.Size == size ? source : DpiHelper.ResizeImage(source, size);
					_images.Add(dpi, image);
				}
				_setImage(image);
			}

			private readonly Dictionary<int, Image> _images = new Dictionary<int, Image>();
		}
	}
}

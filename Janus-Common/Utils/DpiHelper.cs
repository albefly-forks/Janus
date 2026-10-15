using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;

using Microsoft.Win32;

using JetBrains.Annotations;

namespace Rsdn.Janus
{
	/// <summary>
	/// Вспомогательные методы для поддержки мониторов с высоким DPI.
	/// Все размеры в коде задаются в логических единицах (96 DPI) и пересчитываются
	/// в физические пиксели для DPI конкретного монитора.
	/// </summary>
	public static class DpiHelper
	{
		/// <summary>
		/// DPI, для которого заданы логические размеры (100%).
		/// </summary>
		public const int LogicalDpi = 96;

		private static readonly Lazy<int> _systemDpi = new Lazy<int>(GetSystemDpi);

		/// <summary>
		/// Системный DPI (DPI основного монитора на момент входа в систему).
		/// </summary>
		public static int SystemDpi => _systemDpi.Value;

		/// <summary>
		/// Перевести логический размер в физические пиксели для указанного DPI.
		/// </summary>
		public static int Scale(int value, int dpi)
		{
			return dpi == LogicalDpi ? value : (int)Math.Round(value * (double)dpi / LogicalDpi);
		}

		/// <summary>
		/// Перевести логический размер в физические пиксели для указанного DPI.
		/// </summary>
		public static Size Scale(Size size, int dpi)
		{
			return new Size(Scale(size.Width, dpi), Scale(size.Height, dpi));
		}

		/// <summary>
		/// Коэффициент масштабирования для указанного DPI.
		/// </summary>
		public static float GetScaleFactor(int dpi)
		{
			return dpi / (float)LogicalDpi;
		}

		/// <summary>
		/// DPI монитора, на котором находится контрол.
		/// </summary>
		public static int GetDpi([CanBeNull] Control control)
		{
			return control != null ? control.DeviceDpi : SystemDpi;
		}

		/// <summary>
		/// DPI монитора, на котором находится указанная точка экрана.
		/// </summary>
		public static int GetDpiAtPoint(Point screenPoint)
		{
			try
			{
				// Windows 8.1+
				var monitor = MonitorFromPoint(screenPoint, _monitorDefaultToNearest);
				uint dpiX, dpiY;
				if (monitor != IntPtr.Zero
						&& GetDpiForMonitor(monitor, _mdtEffectiveDpi, out dpiX, out dpiY) == 0
						&& dpiX > 0)
					return (int)dpiX;
			}
			catch (DllNotFoundException) {}
			catch (EntryPointNotFoundException) {}
			return SystemDpi;
		}

		/// <summary>
		/// DPI монитора, на котором находится курсор мыши
		/// (там показываются контекстные меню).
		/// </summary>
		public static int GetDpiAtCursor()
		{
			return GetDpiAtPoint(Cursor.Position);
		}

		/// <summary>
		/// Подписаться на смену DPI контрола (перенос окна на другой монитор,
		/// смена масштаба в настройках Windows), а также на создание его окна,
		/// после которого DPI становится известен точно.
		/// </summary>
		/// <returns>Объект, отменяющий подписку.</returns>
		public static IDisposable SubscribeDpiChanged([NotNull] Control control, [NotNull] Action<int> handler)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			var lastDpi = control.DeviceDpi;
			EventHandler onChanged =
				(s, e) =>
				{
					var dpi = control.DeviceDpi;
					if (dpi == lastDpi)
						return;
					lastDpi = dpi;
					handler(dpi);
				};
			DpiChangedEventHandler onFormDpiChanged = (s, e) => onChanged(s, e);

			// Форма верхнего уровня получает DpiChanged, дочерняя (в т.ч. пристыкованная) -
			// DpiChangedAfterParent; при переносе панели в другое окно меняется родитель.
			var form = control as Form;
			control.HandleCreated += onChanged;
			control.ParentChanged += onChanged;
			control.DpiChangedAfterParent += onChanged;
			if (form != null)
				form.DpiChanged += onFormDpiChanged;

			return Disposable.Create(
				() =>
				{
					control.HandleCreated -= onChanged;
					control.ParentChanged -= onChanged;
					control.DpiChangedAfterParent -= onChanged;
					if (form != null)
						form.DpiChanged -= onFormDpiChanged;
				});
		}

		/// <summary>
		/// Масштабировать ширины колонок <see cref="ListView"/> под DPI его монитора.
		/// Автомасштабирование WinForms ширины колонок не трогает, поэтому заданные
		/// в дизайнере (логические) ширины пересчитываются при создании окна и смене DPI.
		/// </summary>
		public static void EnableColumnScaling([NotNull] ListView listView)
		{
			if (listView == null)
				throw new ArgumentNullException(nameof(listView));

			var columnsDpi = LogicalDpi;
			Action rescale =
				() =>
				{
					var dpi = listView.DeviceDpi;
					if (dpi == columnsDpi)
						return;
					foreach (ColumnHeader column in listView.Columns)
						// Отрицательные значения - автоподбор ширины, их не трогаем
						if (column.Width > 0)
							column.Width = column.Width * dpi / columnsDpi;
					columnsDpi = dpi;
				};
			listView.HandleCreated += (s, e) => rescale();
			listView.DpiChangedAfterParent += (s, e) => rescale();
			listView.ParentChanged += (s, e) => rescale();
			if (listView.IsHandleCreated)
				rescale();
		}

		/// <summary>
		/// Включить масштабирование колонок для всех <see cref="ListView"/> контрола
		/// (кроме наследников, которые масштабируют колонки сами).
		/// </summary>
		public static void EnableColumnScalingForChildren([NotNull] Control root)
		{
			if (root == null)
				throw new ArgumentNullException(nameof(root));

			foreach (Control child in root.Controls)
			{
				var listView = child as ListView;
				if (listView != null && listView.GetType() == typeof(ListView))
					EnableColumnScaling(listView);
				var grid = child as DataGridView;
				if (grid != null)
					EnableColumnScaling(grid);
				EnableColumnScalingForChildren(child);
			}
		}

		/// <summary>
		/// Масштабировать ширины колонок и высоту строк <see cref="DataGridView"/> под DPI его монитора.
		/// </summary>
		public static void EnableColumnScaling([NotNull] DataGridView grid)
		{
			if (grid == null)
				throw new ArgumentNullException(nameof(grid));

			var gridDpi = LogicalDpi;
			Action rescale =
				() =>
				{
					var dpi = grid.DeviceDpi;
					if (dpi == gridDpi)
						return;
					foreach (DataGridViewColumn column in grid.Columns)
						if (column.AutoSizeMode != DataGridViewAutoSizeColumnMode.Fill)
							column.Width = column.Width * dpi / gridDpi;
					grid.RowTemplate.Height = grid.RowTemplate.Height * dpi / gridDpi;
					grid.ColumnHeadersHeight = Math.Max(1, grid.ColumnHeadersHeight * dpi / gridDpi);
					gridDpi = dpi;
				};
			grid.HandleCreated += (s, e) => rescale();
			grid.DpiChangedAfterParent += (s, e) => rescale();
			grid.ParentChanged += (s, e) => rescale();
			if (grid.IsHandleCreated)
				rescale();
		}

		private static readonly Dictionary<KeyValuePair<Font, int>, Font> _dpiFonts =
			new Dictionary<KeyValuePair<Font, int>, Font>();

		/// <summary>
		/// Шрифт для рисования на мониторе с заданным DPI.
		/// GDI (TextRenderer) переводит пункты в пиксели по системному DPI, поэтому шрифт,
		/// не принадлежащий контролу, на мониторе с другим DPI нужно пересчитать -
		/// так же WinForms пересчитывает шрифты контролов.
		/// </summary>
		[NotNull]
		public static Font GetFontForDpi([NotNull] Font font, int dpi)
		{
			if (font == null)
				throw new ArgumentNullException(nameof(font));
			if (dpi == SystemDpi)
				return font;

			var key = new KeyValuePair<Font, int>(font, dpi);
			lock (_dpiFonts)
			{
				Font result;
				if (!_dpiFonts.TryGetValue(key, out result))
				{
					result = new Font(font.FontFamily, font.Size * dpi / SystemDpi, font.Style, font.Unit);
					_dpiFonts.Add(key, result);
				}
				return result;
			}
		}

		/// <summary>
		/// Качественно отмасштабировать картинку до указанного размера.
		/// </summary>
		[NotNull]
		public static Bitmap ResizeImage([NotNull] Image image, Size size)
		{
			if (image == null)
				throw new ArgumentNullException(nameof(image));

			var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
			using (var g = Graphics.FromImage(bmp))
			using (var attrs = new ImageAttributes())
			{
				g.CompositingMode = CompositingMode.SourceCopy;
				g.CompositingQuality = CompositingQuality.HighQuality;
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.PixelOffsetMode = PixelOffsetMode.HighQuality;
				// Без TileFlipXY по краям картинки появляется полупрозрачная кайма
				attrs.SetWrapMode(WrapMode.TileFlipXY);
				g.DrawImage(
					image,
					new Rectangle(Point.Empty, size),
					0, 0, image.Width, image.Height,
					GraphicsUnit.Pixel,
					attrs);
			}
			return bmp;
		}

		/// <summary>
		/// Включить масштабирование страниц в <see cref="WebBrowser"/> под DPI.
		/// Встроенный Internet Explorer по умолчанию рисует страницы в 96 DPI, и в
		/// DPI-aware приложении текст страниц выглядит мелким. Настройка FEATURE_96DPI_PIXEL
		/// задается для процесса в реестре пользователя и должна быть установлена
		/// до создания первого браузера.
		/// </summary>
		public static void EnableWebBrowserDpiScaling()
		{
			const string featureKey =
				@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_96DPI_PIXEL";
			try
			{
				var processName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
				using (var key = Registry.CurrentUser.CreateSubKey(featureKey))
				{
					if (key == null)
						return;
					var current = key.GetValue(processName);
					if (!(current is int) || (int)current != 1)
						key.SetValue(processName, 1, RegistryValueKind.DWord);
				}
			}
			catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException || e is IOException)
			{
				// Без настройки страницы будут мельче, но в остальном работают
				Trace.WriteLine("Could not enable WebBrowser DPI scaling: " + e.Message);
			}
		}

		private static int GetSystemDpi()
		{
			try
			{
				// Windows 10 1607+
				var dpi = (int)GetDpiForSystem();
				if (dpi > 0)
					return dpi;
			}
			catch (EntryPointNotFoundException) {}

			using (var g = Graphics.FromHwnd(IntPtr.Zero))
				return (int)Math.Round(g.DpiX);
		}

		private const uint _monitorDefaultToNearest = 2;
		private const int _mdtEffectiveDpi = 0;

		[DllImport("user32.dll")]
		private static extern uint GetDpiForSystem();

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromPoint(Point pt, uint flags);

		[DllImport("shcore.dll")]
		private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
	}
}

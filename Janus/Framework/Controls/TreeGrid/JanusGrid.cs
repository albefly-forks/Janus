using System;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

using Rsdn.TreeGrid;

namespace Rsdn.Janus
{
	/// <summary>
	/// Версия TreeGrid для януса.
	/// </summary>
	public class JanusGrid : TreeGrid.TreeGrid
	{
		#region ColumnsOrder property
		/// <summary>
		/// Порядок следования колонок.
		/// </summary>
		[Browsable(false)]
		public int[] ColumnsOrder
		{
			get
			{
				var res = new int[Columns.Count];

				for (var i = 0; i < Columns.Count; i++)
					res[i] = Columns[i].DisplayIndex;

				return res;
			}
			set
			{
				for (var i = 0; i < value.Length && i < Columns.Count; i++)
					Columns[i].DisplayIndex = value[i];
			}
		}
		#endregion

		#region ColumnsWidth property
		/// <summary>
		/// Ширины колонок в логических единицах (96 DPI) - в таком виде они
		/// сохраняются в настройках и одинаково подходят для мониторов с любым DPI.
		/// </summary>
		[Browsable(false)]
		public int[] ColumnsWidth
		{
			get
			{
				var res = new int[Columns.Count];
				for (var i = 0; i < res.Length; i++)
					res[i] = Columns[i].Width * DpiHelper.LogicalDpi / _metricsDpi;
				return res;
			}
			set
			{
				for (var i = 0; i < value.Length && i < Columns.Count; i++)
					Columns[i].Width = value[i] * _metricsDpi / DpiHelper.LogicalDpi;
			}
		}
		#endregion

		#region Сообщения Windows
		private const int WM_LBUTTONDBLCLK = 0x0203;
		private const int WM_LBUTTONDOWN = 0x0201;
		//private const int WM_LBUTTONUP = 0x0202;
		private const int WM_MBUTTONDBLCLK = 0x0209;
		private const int WM_MBUTTONDOWN = 0x0207;
		//private const int WM_MBUTTONUP = 0x0208;
		//private const int WM_MOUSEMOVE = 0x0200;
		private const int WM_RBUTTONDBLCLK = 0x0206;
		private const int WM_RBUTTONDOWN = 0x0204;
		//private const int WM_RBUTTONUP = 0x0205;
		#endregion

		#region Событие BeforeMouseDownEvent
		public event EventHandler<BeforeMouseDownEventArgs> BeforeMouseDown;

		/// <summary>
		/// Вызывается перед обработкой сообщения нажатия мыши и позволяет
		/// отменить его.
		/// </summary>
		protected virtual bool OnBeforeMouseDownEvent(
			MouseButtons button, int clicks, int x, int y)
		{
			if (BeforeMouseDown != null)
			{
				var e = new BeforeMouseDownEventArgs(
					false, button, clicks, x, y, 0);

				BeforeMouseDown(this, e);

				return e.Cancel;
			}

			return false;
		}
		#endregion

		#region WndProc
		/// <summary>
		/// Обработчик сообщений виндовс.
		/// </summary>
		protected override void WndProc(ref Message m)
		{
			var clicks = 1;

			switch (m.Msg)
			{
				case WM_MBUTTONDBLCLK:
				case WM_LBUTTONDBLCLK:
				case WM_RBUTTONDBLCLK:
					clicks = 2;
					goto case WM_LBUTTONDOWN;
				case WM_LBUTTONDOWN:
				case WM_RBUTTONDOWN:
				case WM_MBUTTONDOWN:
					if (OnBeforeMouseDownEvent(
						MouseButtons, clicks,
						BitUtils.LoWordAsInt(m.LParam),
						BitUtils.HiWordAsInt(m.LParam)))
						return;

					break;
			}

			base.WndProc(ref m);
		}
		#endregion

		#region High DPI
		// DPI, для которого отмасштабирован значок +/-
		private int _metricsDpi = DpiHelper.LogicalDpi;
		private int _logicalIndent = 5;

		/// <summary>
		/// Отступ уровня дерева в логических единицах (96 DPI).
		/// </summary>
		[DefaultValue(5)]
		public new int Indent
		{
			get { return _logicalIndent; }
			set
			{
				_logicalIndent = value;
				base.Indent = DpiHelper.Scale(value, DeviceDpi);
			}
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			UpdateDpiMetrics();
		}

		protected override void OnDpiChangedAfterParent(EventArgs e)
		{
			base.OnDpiChangedAfterParent(e);
			UpdateDpiMetrics();
		}

		protected override void OnParentChanged(EventArgs e)
		{
			base.OnParentChanged(e);
			UpdateDpiMetrics();
		}

		private void UpdateDpiMetrics()
		{
			var dpi = DeviceDpi;
			if (dpi == _metricsDpi)
				return;

			base.Indent = DpiHelper.Scale(_logicalIndent, dpi);
			ScalePlusMinusGlyph(dpi);
			// Автомасштабирование WinForms ширины колонок не трогает
			foreach (ColumnHeader column in Columns)
				if (column.Width > 0)
					column.Width = column.Width * dpi / _metricsDpi;
			_metricsDpi = dpi;
			Invalidate();
		}

		private static readonly FieldInfo _plusMinusImagesField =
			typeof(TreeGrid.TreeGrid).GetField("_plusMinusImages", BindingFlags.Instance | BindingFlags.NonPublic);

		/// <summary>
		/// Размер значка +/- в TreeGrid берется из его закрытого списка картинок,
		/// а сам значок рисуется темой Windows в этот прямоугольник.
		/// </summary>
		private void ScalePlusMinusGlyph(int dpi)
		{
			var images = _plusMinusImagesField?.GetValue(this) as ImageList;
			if (images == null)
				return;
			var size = images.ImageSize;
			images.ImageSize = new Size(size.Width * dpi / _metricsDpi, size.Height * dpi / _metricsDpi);
		}
		#endregion

		#region Overridden methods
		protected override NodeDrawData RetrieveData(ITreeNode node)
		{
			var config = Config.Instance.ForumDisplayConfig;
			NodeDrawData nodeDrawData;
			// Узлы берут картинки у менеджеров картинок, не зная, в каком гриде показываются
			using (ImageDpiScope.Enter(DeviceDpi))
				nodeDrawData = base.RetrieveData(node);


			if (!config.HighlightParentMessage && !config.HighlightChildMessages)
				return nodeDrawData;

			if (node is IMsg)
			{
				var style = Config.Instance.StyleConfig;

				if (ActiveNode != null && ActiveNode.Parent == node)
				{
					if (config.HighlightParentMessage)
						nodeDrawData.NodeInfo.BackColor = style.ParentActiveMessageBackColor;
				}
				else if (node.Parent != null && node.Parent == ActiveNode)
					if (config.HighlightChildMessages)
						nodeDrawData.NodeInfo.BackColor = style.ChildActiveMessageBackColor;
			}

			return nodeDrawData;
		}

		protected override void OnAfterActivateNode(ITreeNode activatedNode)
		{
			base.OnAfterActivateNode(activatedNode);

			if (activatedNode is IMsg)
			{
				var config = Config.Instance.ForumDisplayConfig;

				// Если не обновлять, то при смене активной ветки не перерисовываются
				// ранее подкрашенные ветки
				if (config.HighlightParentMessage || config.HighlightChildMessages)
					Refresh();
			}
		}
		#endregion
	}
}
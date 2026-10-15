using System;

namespace Rsdn.Janus
{
	/// <summary>
	/// Задает DPI, для которого модель отдает картинки, когда контрол запрашивает
	/// данные отрисовки (например, узлы TreeGrid не знают, в каком гриде они показаны).
	/// </summary>
	public static class ImageDpiScope
	{
		[ThreadStatic]
		private static int _current;

		/// <summary>
		/// Текущий DPI картинок. Вне области <see cref="Enter"/> - системный DPI.
		/// </summary>
		public static int Current => _current != 0 ? _current : DpiHelper.SystemDpi;

		/// <summary>
		/// Установить DPI картинок до вызова Dispose у результата.
		/// </summary>
		public static IDisposable Enter(int dpi)
		{
			var scope = new Scope(_current);
			_current = dpi;
			return scope;
		}

		private sealed class Scope : IDisposable
		{
			private readonly int _previous;

			public Scope(int previous)
			{
				_previous = previous;
			}

			public void Dispose()
			{
				_current = _previous;
			}
		}
	}
}

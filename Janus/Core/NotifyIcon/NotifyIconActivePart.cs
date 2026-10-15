using System;
using System.Drawing;
using System.Windows.Forms;

using CodeJam;
using CodeJam.Extensibility;
using CodeJam.Services;
using CodeJam.Strings;

using JetBrains.Annotations;

using Rsdn.Janus.Core.NotifyIcon;

namespace Rsdn.Janus
{
	[ActivePart]
	public class NotifyIconActivePart : ActivePartBase
	{
		private static readonly Icon _trayIconEmpty =
			LoadTrayIcon("JanusSmall.ico");
		private static readonly Icon _trayIconNew =
			LoadTrayIcon("JanusSmallNew.ico");
		private static readonly Icon _trayIconMeUnread =
			LoadTrayIcon("JanusSmallMe.ico");

		/// <summary>
		/// Иконка в размере значка области уведомлений (в .ico есть кадры для всех масштабов).
		/// </summary>
		private static Icon LoadTrayIcon(string name)
		{
			using (var icon = typeof(NotifyIconActivePart).Assembly.LoadIcon(ApplicationInfo.ResourcesNamespace + name))
				return new Icon(icon, SystemInformation.SmallIconSize);
		}

		private readonly INotifyIconService _notifyIconService;
		private readonly IForumsAggregatesService _forumsAggregatesService;
		private IDisposable _aggregatesChangedSubscription;

		public NotifyIconActivePart([NotNull] IServiceProvider serviceProvider)
			: base(serviceProvider)
		{
			_notifyIconService = ServiceProvider.GetService<INotifyIconService>();
			_forumsAggregatesService = ServiceProvider.GetService<IForumsAggregatesService>();
		}

		#region Overrides of ActivePartBase

		protected override void ActivateCore()
		{
			if (_notifyIconService == null)
				return;

			UpdateIconAndTooltip();
			_notifyIconService.ContextMenuName = "Application.TrayMenu";
			_notifyIconService.Enabled = true;
			_notifyIconService.Visible = true;

			_aggregatesChangedSubscription =
				_forumsAggregatesService.AggregatesChanged.Subscribe(args => UpdateIconAndTooltip());
		}

		protected override void PassivateCore()
		{
			if (_aggregatesChangedSubscription != null)
			{
				_aggregatesChangedSubscription.Dispose();
				_aggregatesChangedSubscription = null;
			}
		}

		#endregion

		private void UpdateIconAndTooltip()
		{
			if (_forumsAggregatesService != null)
			{
				var totalUnread = _forumsAggregatesService.UnreadMessagesCount;
				var meUnread = _forumsAggregatesService.UnreadRepliesToMeCount;

				if (totalUnread > 0)
				{
					if (meUnread > 0)
					{
						_notifyIconService.Tooltip = "{0} {1}".FormatWith(
							meUnread,
							meUnread.GetDeclension(
								NotifyIconResources.HintMeMessages1,
								NotifyIconResources.HintMeMessages2,
								NotifyIconResources.HintMeMessages5));
						_notifyIconService.Icon = _trayIconMeUnread;
						return;
					}

					_notifyIconService.Tooltip = "{0} {1}".FormatWith(
						totalUnread,
						totalUnread.GetDeclension(
							NotifyIconResources.HintUnreadMessages1,
							NotifyIconResources.HintUnreadMessages2,
							NotifyIconResources.HintUnreadMessages5));
					_notifyIconService.Icon = _trayIconNew;
					return;
				}
			}
			_notifyIconService.Tooltip = ApplicationInfo.ApplicationName;
			_notifyIconService.Icon = _trayIconEmpty;
		}
	}
}
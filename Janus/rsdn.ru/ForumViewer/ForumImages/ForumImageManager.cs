using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

using CodeJam.Collections;
using CodeJam.Extensibility;
using CodeJam.Services;

namespace Rsdn.Janus
{
	/// <summary>
	/// Картинки деревьев форумов и сообщений. Картинки отдаются для DPI,
	/// заданного <see cref="ImageDpiScope"/> (его устанавливает грид при отрисовке).
	/// </summary>
	[Service(typeof (IForumImageManager))]
	internal class ForumImageManager : IForumImageManager
	{
		private readonly IStyleImageManager _styler;
		private readonly Dictionary<int, DpiImageSet> _sets = new Dictionary<int, DpiImageSet>();

		public ForumImageManager(IServiceProvider provider)
		{
			_styler = provider.GetRequiredService<IStyleImageManager>();
		}

		private DpiImageSet CurrentSet
		{
			get
			{
				var dpi = ImageDpiScope.Current;
				DpiImageSet set;
				if (!_sets.TryGetValue(dpi, out set))
				{
					set = new DpiImageSet(_styler, dpi);
					_sets.Add(dpi, set);
				}
				return set;
			}
		}

		#region DpiImageSet class
		/// <summary>
		/// Картинки для одного значения DPI.
		/// </summary>
		private sealed class DpiImageSet
		{
			private const int _logicalImageSize = 16;

			private readonly int _imageSize;

			private readonly Image _msgImage;
			private readonly Image _msgUnreadImage;
			private readonly Image _msgArticleImage;
			private readonly Image _msgArticleUnreadImage;
			private readonly Image _mdrImage;
			private readonly Image _mdrRepliesImage;
			private readonly Image _closedImage;
			private readonly Image _unreadRepliesToMeImage;

			private readonly Image[] _dayOfWeekImages = new Image[7];
			private readonly Image[] _outdatedDayOfWeekImages = new Image[7];

			private readonly Dictionary<UserClass, Image> _userImages = new Dictionary<UserClass, Image>();

			private readonly Image _forumImage;
			private readonly Image _forumUnreadImage;
			private readonly Image _inTopImage;

			public DpiImageSet(IStyleImageManager styler, int dpi)
			{
				_imageSize = DpiHelper.Scale(_logicalImageSize, dpi);

				const string prefix = @"MessageTree\";
				Func<string, Image> get = name => styler.GetImage(name, StyleImageType.ConstSize, dpi);

				_msgImage = get(prefix + "Msg");
				_msgUnreadImage = get(prefix + "MsgUnread");
				_msgArticleImage = get(prefix + "MsgArticle");
				_msgArticleUnreadImage = get(prefix + "MsgArticleUnread");

				_mdrImage = get(prefix + "Moderatorial");
				_mdrRepliesImage = get(prefix + "Moderatorials");

				_closedImage = get(prefix + "Closed");
				_unreadRepliesToMeImage = get(prefix + "UnreadRepliesToMe");

				for (var i = 0; i < 7; i++)
				{
					var si = i.ToString();
					_dayOfWeekImages[i] = get(prefix + "WD" + si);
					_outdatedDayOfWeekImages[i] = get(prefix + "WDOUT" + si);
				}

				//иконки для классов пользователей
				_userImages[UserClass.User] = get(prefix + "User");
				_userImages[UserClass.Anonym] = get(prefix + "UserAnonym");
				_userImages[UserClass.Team] = get(prefix + "UserTeam");
				_userImages[UserClass.Moderator] = get(prefix + "UserModerator");
				_userImages[UserClass.Admin] = get(prefix + "UserAdmin");
				_userImages[UserClass.Expert] = get(prefix + "UserExpert");
				_userImages[UserClass.Group] = get(prefix + "UserGroup");

				MarkImage = get(prefix + "Mark");
				MarksImage = get(prefix + "Marks");

				AutoReadImage = get(prefix + "ReadReplies");

				const string navPrefix = @"NavTree\";
				_forumImage = get(navPrefix + "Forum");
				_forumUnreadImage = get(navPrefix + "ForumUnread");
				_inTopImage = get(navPrefix + "InTopSign");

				MsgImages = LazyDictionary.Create<MsgImageKey, Image>(CreateMsgImage, true);
				ForumImages = LazyDictionary.Create<ForumImageKey, Image>(CreateForumImage, true);
			}

			public ILazyDictionary<MsgImageKey, Image> MsgImages { get; }
			public ILazyDictionary<ForumImageKey, Image> ForumImages { get; }
			public Image MarkImage { get; }
			public Image MarksImage { get; }
			public Image AutoReadImage { get; }

			public Image GetUserImage(UserClass userClass)
			{
				return _userImages[userClass];
			}

			public Image GetMsgDateImage(int index, bool outdated)
			{
				if (index < 0 || index > 6)
					throw new ArgumentOutOfRangeException(nameof(index));
				return (outdated ? _outdatedDayOfWeekImages : _dayOfWeekImages)[index];
			}

			private Bitmap Compose(Image baseImage, params Image[] overlays)
			{
				var bmp = new Bitmap(_imageSize, _imageSize, PixelFormat.Format32bppArgb);
				using (var grp = Graphics.FromImage(bmp))
				{
					grp.CompositingMode = CompositingMode.SourceOver;
					grp.CompositingQuality = CompositingQuality.HighQuality;
					grp.InterpolationMode = InterpolationMode.HighQualityBicubic;
					grp.DrawImage(baseImage, 0, 0, _imageSize, _imageSize);
					foreach (var overlay in overlays)
						if (overlay != null)
							grp.DrawImage(overlay, 0, 0, _imageSize, _imageSize);
				}
				return bmp;
			}

			private Image CreateMsgImage(MsgImageKey key)
			{
				var baseImage =
					key.Type == MessageType.Ordinal
						? key.HasUnreaded == MessageFlagExistence.None
							? _msgImage
							: _msgUnreadImage
						: key.HasUnreaded == MessageFlagExistence.None
							? _msgArticleImage
							: _msgArticleUnreadImage;
				if (!key.HasUnreadRepliesToMe && key.HasModeratorials == MessageFlagExistence.None && !key.Closed)
					return baseImage;
				return Compose(
					baseImage,
					key.Closed ? _closedImage : null,
					key.HasModeratorials == MessageFlagExistence.None
						? null
						: key.HasModeratorials == MessageFlagExistence.OnMessage
							? _mdrImage
							: _mdrRepliesImage,
					key.HasUnreadRepliesToMe ? _unreadRepliesToMeImage : null);
			}

			private Image CreateForumImage(ForumImageKey key)
			{
				var baseImage = key.HasUnread ? _forumUnreadImage : _forumImage;
				if (!key.InTop && !key.HasUnreadRepliesToMe)
					return baseImage;
				return Compose(
					baseImage,
					key.InTop ? _inTopImage : null,
					key.HasUnreadRepliesToMe ? _unreadRepliesToMeImage : null);
			}
		}
		#endregion

		#region IForumImageManager Members
		public Image GetMessageImage(
			MessageType type,
			MessageFlagExistence hasUnreaded,
			bool hasUnreadRepliesToMe,
			MessageFlagExistence hasModeratorials,
			bool closed)
		{
			return
				CurrentSet.MsgImages[new MsgImageKey(type, hasUnreaded, hasUnreadRepliesToMe, hasModeratorials, closed)];
		}

		private static bool IsMsgOutdated(DateTime date)
		{
			var daysToOutdate = Config.Instance.ForumDisplayConfig.DaysToOutdate;
			return DateTime.Now.AddDays(-daysToOutdate) > date && daysToOutdate != 0;
		}

		public Image GetMessageDateImage(DateTime date)
		{
			return CurrentSet.GetMsgDateImage((int)date.DayOfWeek, IsMsgOutdated(date));
		}

		public Image GetUserImage(UserClass userClass)
		{
			return CurrentSet.GetUserImage(userClass);
		}

		public Image GetMarkImage(MessageFlagExistence existence)
		{
			switch (existence)
			{
				case MessageFlagExistence.None:
					return null;
				case MessageFlagExistence.OnMessage:
					return CurrentSet.MarkImage;
				case MessageFlagExistence.OnChildren:
					return CurrentSet.MarksImage;
				default:
					throw new ArgumentOutOfRangeException(nameof(existence));
			}
		}

		public Image GetAutoReadImage(MessageFlagExistence existence)
		{
			return existence == MessageFlagExistence.None ? null : CurrentSet.AutoReadImage;
		}

		public Image GetForumImage(bool hasUnread, bool inTop, bool hasUnreadRepliesToMe)
		{
			return CurrentSet.ForumImages[new ForumImageKey(hasUnread, inTop, hasUnreadRepliesToMe)];
		}
		#endregion

		#region MsgImageKey struct
		private struct MsgImageKey : IEquatable<MsgImageKey>
		{
			private readonly MessageType _type;
			private readonly MessageFlagExistence _hasUnreaded;
			private readonly bool _hasUnreadRepliesToMe;
			private readonly MessageFlagExistence _hasModeratorials;
			private readonly bool _closed;

			public MsgImageKey(
				MessageType type,
				MessageFlagExistence hasUnreaded,
				bool hasUnreadRepliesToMe,
				MessageFlagExistence hasModeratorials,
				bool closed)
			{
				_type = type;
				_hasUnreaded = hasUnreaded;
				_hasUnreadRepliesToMe = hasUnreadRepliesToMe;
				_hasModeratorials = hasModeratorials;
				_closed = closed;
			}

			public MessageType Type => _type;

			public MessageFlagExistence HasUnreaded => _hasUnreaded;

			public bool HasUnreadRepliesToMe => _hasUnreadRepliesToMe;

			public MessageFlagExistence HasModeratorials => _hasModeratorials;

			public bool Closed => _closed;


			public static bool operator !=(MsgImageKey msgImageKey1, MsgImageKey msgImageKey2)
			{
				return !msgImageKey1.Equals(msgImageKey2);
			}

			public static bool operator ==(MsgImageKey msgImageKey1, MsgImageKey msgImageKey2)
			{
				return msgImageKey1.Equals(msgImageKey2);
			}

			public bool Equals(MsgImageKey msgImageKey)
			{
				if (!Equals(_type, msgImageKey._type))
					return false;
				if (!Equals(_hasUnreaded, msgImageKey._hasUnreaded))
					return false;
				if (!Equals(_hasUnreadRepliesToMe, msgImageKey._hasUnreadRepliesToMe))
					return false;
				if (!Equals(_hasModeratorials, msgImageKey._hasModeratorials))
					return false;
				if (!Equals(_closed, msgImageKey._closed))
					return false;
				return true;
			}

			public override bool Equals(object obj)
			{
				if (!(obj is MsgImageKey))
					return false;
				return Equals((MsgImageKey)obj);
			}

			public override int GetHashCode()
			{
				var result = _type.GetHashCode();
				result = 29 * result + _hasUnreaded.GetHashCode();
				result = 29 * result + _hasUnreadRepliesToMe.GetHashCode();
				result = 29 * result + _hasModeratorials.GetHashCode();
				result = 29 * result + _closed.GetHashCode();
				return result;
			}
		}
		#endregion

		#region ForumImageKey struct
		private struct ForumImageKey : IEquatable<ForumImageKey>
		{
			private readonly bool _hasUnread;
			private readonly bool _inTop;
			private readonly bool _hasUnreadRepliesToMe;

			public ForumImageKey(bool hasUnread, bool inTop, bool hasUnreadRepliesToMe)
			{
				_hasUnread = hasUnread;
				_inTop = inTop;
				_hasUnreadRepliesToMe = hasUnreadRepliesToMe;
			}

			public bool HasUnread => _hasUnread;

			public bool InTop => _inTop;

			public bool HasUnreadRepliesToMe => _hasUnreadRepliesToMe;

			public static bool operator !=(ForumImageKey forumImageKey1, ForumImageKey forumImageKey2)
			{
				return !forumImageKey1.Equals(forumImageKey2);
			}

			public static bool operator ==(ForumImageKey forumImageKey1, ForumImageKey forumImageKey2)
			{
				return forumImageKey1.Equals(forumImageKey2);
			}

			public bool Equals(ForumImageKey forumImageKey)
			{
				if (!Equals(_hasUnread, forumImageKey._hasUnread))
					return false;
				return Equals(_inTop, forumImageKey._inTop) && Equals(_hasUnreadRepliesToMe, forumImageKey._hasUnreadRepliesToMe);
			}

			public override bool Equals(object obj)
			{
				if (!(obj is ForumImageKey))
					return false;
				return Equals((ForumImageKey)obj);
			}

			public override int GetHashCode()
			{
				var result = _hasUnread.GetHashCode();
				result = 29 * result + _inTop.GetHashCode();
				result = 29 * result + _hasUnreadRepliesToMe.GetHashCode();
				return result;
			}
		}
		#endregion
	}
}

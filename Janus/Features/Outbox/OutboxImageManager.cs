using System.Windows.Forms;

using CodeJam.Services;

namespace Rsdn.Janus
{
	// TODO : убить статику, заменить на сервис.
	/// <summary>
	/// Управляет картинками.
	/// </summary>
	public class OutboxImageManager
	{
		private static DpiImageList _imageList;

		private static int _msgImageIndex;
		private static int _msgWaitImageIndex;
		private static int _msgReplyImageIndex;
		private static int _msgWaitReplyImageIndex;
		private static int _msgFolderImageIndex;
		private static int _rate1ImageIndex;
		private static int _rate2ImageIndex;
		private static int _rate3ImageIndex;
		private static int _rateSmileImageIndex;
		private static int _rateDisagreeImageIndex;
		private static int _rateAgreeImageIndex;
		private static int _ratePlus1ImageIndex;
		private static int _rateDeleteImageIndex;
		private static int _rateFolderImageIndex;
		private static int _regetTopicImageIndex;
		private static int _regetTopicFolderImageIndex;

		public static DpiImageList ImageList
		{
			get 
			{
				if (_imageList == null)
					InitImageList();
				return _imageList; 
			}
		}

		public static int MsgImageIndex => _msgImageIndex;

		public static int MsgWaitImageIndex => _msgWaitImageIndex;

		public static int MsgReplyImageIndex => _msgReplyImageIndex;

		public static int MsgWaitReplyImageIndex => _msgWaitReplyImageIndex;

		public static int MsgFolderImageIndex => _msgFolderImageIndex;

		public static int Rate1ImageIndex => _rate1ImageIndex;

		public static int Rate2ImageIndex => _rate2ImageIndex;

		public static int Rate3ImageIndex => _rate3ImageIndex;

		public static int RateSmileImageIndex => _rateSmileImageIndex;

		public static int RateDisagreeImageIndex => _rateDisagreeImageIndex;

		public static int RateAgreeImageIndex => _rateAgreeImageIndex;

		public static int RatePlus1ImageIndex => _ratePlus1ImageIndex;

		public static int RateDeleteImageIndex => _rateDeleteImageIndex;

		public static int RateFolderImageIndex => _rateFolderImageIndex;

		public static int RegetTopicImageIndex => _regetTopicImageIndex;

		public static int RegetTopicFolderImageIndex => _regetTopicFolderImageIndex;

		private static void InitImageList()
		{
			const string prefix = @"Outbox\";

			_imageList = new DpiImageList();

			var styleImageManager =
				ApplicationManager.Instance.ServiceProvider.GetRequiredService<IStyleImageManager>();
			_msgImageIndex =
				_imageList.Add(styleImageManager, prefix + "Msg", StyleImageType.ConstSize);
			_msgWaitImageIndex =
				_imageList.Add(styleImageManager, prefix + "MsgWait", StyleImageType.ConstSize);
			_msgReplyImageIndex =
				_imageList.Add(styleImageManager, prefix + "MsgReply", StyleImageType.ConstSize);
			_msgWaitReplyImageIndex =
				_imageList.Add(styleImageManager, prefix + "MsgWaitReply", StyleImageType.ConstSize);
			_msgFolderImageIndex =
				_imageList.Add(styleImageManager, prefix + "MsgFolder", StyleImageType.ConstSize);
			_rate1ImageIndex =
				_imageList.Add(styleImageManager, prefix + "Rate1", StyleImageType.ConstSize);
			_rate2ImageIndex =
				_imageList.Add(styleImageManager, prefix + "Rate2", StyleImageType.ConstSize);
			_rate3ImageIndex =
				_imageList.Add(styleImageManager, prefix + "Rate3", StyleImageType.ConstSize);
			_rateAgreeImageIndex =
				_imageList.Add(styleImageManager, prefix + "Rate-4", StyleImageType.ConstSize);
			_rateDisagreeImageIndex =
				_imageList.Add(styleImageManager, prefix + "Rate0", StyleImageType.ConstSize);
			_rateSmileImageIndex =
				_imageList.Add(styleImageManager, prefix + "Rate-2", StyleImageType.ConstSize);
			_ratePlus1ImageIndex =
				_imageList.Add(styleImageManager, prefix + "Rate-3", StyleImageType.ConstSize);
			_rateDeleteImageIndex =
				_imageList.Add(styleImageManager, prefix + "Rate-1", StyleImageType.ConstSize);
			_rateFolderImageIndex =
				_imageList.Add(styleImageManager, prefix + "RateFolder", StyleImageType.ConstSize);
			_regetTopicImageIndex =
				_imageList.Add(styleImageManager, "RegetTopic", StyleImageType.Small);
			_regetTopicFolderImageIndex =
				_imageList.Add(styleImageManager, prefix + "RegetTopicFolder", StyleImageType.ConstSize);
		}
	}
}
using System;
using System.Windows.Forms;

using CodeJam.Services;

namespace Rsdn.Janus
{
	/// <summary>
	/// Summary description for RateForm.
	/// </summary>
	public partial class RateForm : Form
	{
		private readonly IServiceProvider _provider;

		public RateForm(IServiceProvider provider, MessageRates rate)
		{
			_provider = provider;
			InitializeComponent();
			CustomInitializeComponents(rate);
		}

		private void CustomInitializeComponents(MessageRates rate)
		{
			_lblInfo.Text = SR.Forum.RateForm.AddRate;

			string imageName = null;
			switch (rate)
			{
				case MessageRates.Plus1:
					imageName = "MessageViewer\\RatePlus1";
					break;
				case MessageRates.Agree:
					imageName = "MessageViewer\\RateAgree";
					break;
				case MessageRates.DisAgree:
					imageName = "MessageViewer\\RateDisagree";
					break;
				case MessageRates.Rate1:
					imageName = "MessageViewer\\Rate1";
					break;
				case MessageRates.Rate2:
					imageName = "MessageViewer\\Rate2";
					break;
				case MessageRates.Rate3:
					imageName = "MessageViewer\\Rate3";
					break;
				case MessageRates.Smile:
					imageName = "MessageViewer\\RateSmile";
					break;
				case MessageRates.DeleteRate:
					imageName = "MessageViewer\\RateX";
					_lblInfo.Text = SR.Forum.RateForm.DeleteRate;
					break;
				case MessageRates.DeleteLocally:
					_lblInfo.Text = SR.Forum.RateForm.DeleteRateLocally;
					break;
			}

			if (imageName != null)
				_provider
					.GetRequiredService<IStyleImageManager>()
					.BindImage(_pbxRateImage, imageName, StyleImageType.Small, img => _pbxRateImage.Image = img);
		}
	}
}
using System;
using System.Windows.Forms;

using CodeJam.Services;

using Rsdn.Janus.ObjectModel;

namespace Rsdn.Janus
{
	/// <summary>
	/// Дефолтная реализация гуя фичи с вложенными узлами.
	/// </summary>
	public partial class FolderDummyForm : UserControl, IFeatureView
	{
		public FolderDummyForm(IServiceProvider provider, IFeature folderFeature)
		{
			if (provider == null)
				throw new ArgumentNullException(nameof(provider));
			if (folderFeature == null)
				throw new ArgumentNullException(nameof(folderFeature));

			InitializeComponent();

			provider
				.GetRequiredService<IStyleImageManager>()
				.BindImage(_pictureBox, "folder", StyleImageType.ConstSize, img => _pictureBox.Image = img);
			_label.Text = folderFeature.Description;
		}

		#region IFeatureView Members

		void IFeatureView.Activate(IFeature feature) { }

		void IFeatureView.Refresh() { }

		void IFeatureView.ConfigChanged() { }

		#endregion
	}
}

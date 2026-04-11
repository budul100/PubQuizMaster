using System;
using Avalonia.Controls;
using PubQuizMaster.Desktop.ViewModels;

namespace PubQuizMaster.Desktop.Views
{
    public partial class RoundMatrixView
        : UserControl
    {
        #region Public Constructors

        public RoundMatrixView()
        {
            InitializeComponent();
        }

        #endregion Public Constructors

        #region Protected Methods

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is RoundMatrixViewModel vm)
            {
                vm.PickTemplateFileAsync = async () =>
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel == null) return null;

                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                        new Avalonia.Platform.Storage.FilePickerOpenOptions
                        {
                            Title = "Select PPTX Template",
                            AllowMultiple = false,
                            FileTypeFilter = new[]
                            {
                                new Avalonia.Platform.Storage.FilePickerFileType("PowerPoint")
                        {
                            Patterns = new[] { "*.pptx" }
                        }
                            }
                        });

                    return files.Count > 0 ? files[0].Path.LocalPath : null;
                };
            }
        }

        #endregion Protected Methods
    }
}
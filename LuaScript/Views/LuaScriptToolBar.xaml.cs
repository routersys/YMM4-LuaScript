using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using YukkuriMovieMaker.Commons;

namespace LuaScript
{
    public partial class LuaScriptToolBar : UserControl, IPropertyEditorControl
    {
        private readonly LuaScriptToolBarViewModel _viewModel = new();

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ItemProperty[]? ItemProperties
        {
            get => _viewModel.ItemProperties;
            set => _viewModel.ItemProperties = value;
        }

        public LuaScriptToolBar()
        {
            InitializeComponent();
            _viewModel.BeginEdit += (_, e) => BeginEdit?.Invoke(this, e);
            _viewModel.EndEdit += (_, e) => EndEdit?.Invoke(this, e);
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.HasProviders) return;

            var dlg = new OpenFileDialog
            {
                Filter = Texts.LuaFileFilter,
                DefaultExt = ".lua"
            };
            if (dlg.ShowDialog() != true) return;

            string script;
            try
            {
                script = File.ReadAllText(dlg.FileName, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(ex.Message, Texts.ToolBarImportErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _viewModel.ApplyScript(script);
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var script = _viewModel.FirstScript;
            if (script is null) return;

            var dlg = new SaveFileDialog
            {
                Filter = Texts.LuaFileFilter,
                DefaultExt = ".lua",
                FileName = "script.lua"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dlg.FileName, script, new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(ex.Message, Texts.ToolBarExportErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.HasProviders) return;

            if (MessageBox.Show(Texts.ToolBarClearConfirm, Texts.ToolBarClearTitle, MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                return;

            _viewModel.ResetToDefault();
        }
    }
}

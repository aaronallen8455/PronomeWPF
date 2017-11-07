using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TaskDialogInterop;

namespace Pronome
{
    public class SaveFileHelper
    {
        public RecentlyOpenedFiles RecentFiles;

        public FileInfo CurrentFile;

        public SaveFileHelper(RecentlyOpenedFiles recentlyOpenedFiles)
        {
            RecentFiles = recentlyOpenedFiles;
        }

        /// <summary>
        /// Save a new file. Opens the save file dialog.
        /// </summary>
        public void SaveFileAs()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.AddExtension = true;
            saveFileDialog.DefaultExt = "beat";
            saveFileDialog.ValidateNames = true;
            saveFileDialog.Title = "Save Beat As";
            saveFileDialog.Filter = "Beat file (*.beat)|*.beat";

            if (saveFileDialog.ShowDialog() == true)
            {
                SaveFile(saveFileDialog.FileName);
            }
        }

        /// <summary>
        /// Save the beat to the given file URI
        /// </summary>
        /// <param name="uri"></param>
        public void SaveFile(string uri)
        {
            Metronome.Save(uri);

            if (CurrentFile == null)
            {
                var file = new FileInfo() { Uri = uri, Name = System.IO.Path.GetFileName(uri) };
                AddToRecentFiles(file);
                CurrentFile = file;
            }
        }

        /// <summary>
        /// Loads a file by opening a dialog.
        /// </summary>
        public void LoadFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Beat file (*.beat)|*.beat";
            openFileDialog.Title = "Open Beat";
            openFileDialog.DefaultExt = "beat";

            if (openFileDialog.ShowDialog() == true)
            {
                LoadFileUri(openFileDialog.FileName);
            }
        }


        /// <summary>
        /// Loads a file from the given URI
        /// </summary>
        /// <param name="uri"></param>
        public void LoadFileUri(string uri)
        {
            var file = new FileInfo() { Uri = uri, Name = System.IO.Path.GetFileName(uri) };

            if (System.IO.File.Exists(uri))
            {
                AddToRecentFiles(file);
                CurrentFile = file;

                Metronome.Load(uri);
            }
            else
            {
                TaskDialog.ShowMessage(Application.Current.MainWindow, "File Not Found",
                    "That file doesn't exist!", null, null, null, null,
                    TaskDialogCommonButtons.Close, VistaTaskDialogIcon.Error, VistaTaskDialogIcon.None);

                RecentFiles.Remove(file);
            }
        }

        protected void AddToRecentFiles(FileInfo file)
        {
            // move it the front of the list if already present.
            RecentFiles.Remove(file);
            RecentFiles.Insert(0, file);
        }

        private void recentFilesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combobox = sender as ComboBox;
            string uri = combobox.SelectedValue as string;

            if (System.IO.File.Exists(uri))
            {
                //OpenFile(uri);
            }
            else
            {
                TaskDialog.ShowMessage(Application.Current.MainWindow, "File Not Found",
                    "That file no longer exists!", null, null, null, null,
                    TaskDialogCommonButtons.Close, VistaTaskDialogIcon.Error, VistaTaskDialogIcon.None);

                // remove the missing file from the list
                RecentFiles.Remove(combobox.SelectedItem as FileInfo);
            }
        }
    }
}

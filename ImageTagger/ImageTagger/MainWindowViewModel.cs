using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace ImageTagger
{
    internal class MainWindowViewModel : INotifyPropertyChanged
    {
        private string? folderPath;
        private bool isBusy;
        private int _totalImages;
        private int _currentProgress;
        private List<string> predefinedIgnoreList = new List<string>();
        private string status;
        private int succesCount, failCount;
        private List<string> combinedIgnoreList;
        private ImageModel previewImage;

        public string? FolderPath 
        {
            get
            {
                return folderPath;
            }
            set
            {
                folderPath = value;
                Notify(nameof(FolderPath));
            }
        }

        public bool IsBusy
        {
            get
            {
                return isBusy;
            }
            set
            {
                isBusy = value;
                Notify(nameof(IsBusy));
            }
        }

        public int TotalImages 
        {
            get
            {
                return _totalImages;
            }
            set
            {
                _totalImages = value;
                Notify(nameof(TotalImages));
            }
        }

        public int CurrentProgress 
        {
            get
            {
                return _currentProgress;
            }
            set
            {
                _currentProgress = value;
                Notify(nameof(CurrentProgress));
            }
        }

        public string Status 
        {
            get
            {
                return status;
            }
            set
            {
                status = value;
                Notify(nameof(Status));
            }
        }

        public ImageModel PreviewImage 
        {
            get { return previewImage; }
            set { previewImage = value; }
        }

        public RelayCommand BrowseFolderCommand { get; set; }
        public RelayCommand LoadFilesCommand { get; set; }
        public RelayCommand UpdateCommand { get; set; }
        public RelayCommand AddIgnoreTagCommand { get; set; }

        public ObservableCollection<string> IgnoreList { get; set; }

        public ObservableCollection<FolderModel> Folders { get; set; }

        public ObservableCollection<string> Logs { get; set; }

        public MainWindowViewModel()
        {
            IgnoreList = new ObservableCollection<string>();
            Folders = new ObservableCollection<FolderModel>();
            Logs = new ObservableCollection<string>();

            BrowseFolderCommand = new RelayCommand(ExecuteBrowseFolderCommand);
            LoadFilesCommand = new RelayCommand(ExecuteLoadFilesCommand, CanExecuteLoadFilesCommand);
            UpdateCommand = new RelayCommand(ExecuteUpdateCommand, CanExecuteUpdateCommand);
            AddIgnoreTagCommand = new RelayCommand(ExecuteAddIgnoreTagCommand);

            Broadcaster.Subscribe(Broadcaster.PreviewEvent, HandlePreviewEvent);
            ReadUserDefinedIgnoreList();
        }

        private void HandlePreviewEvent(object obj)
        {
            ImageModel image = obj as ImageModel;

            if(image == null)
                return;

            PreviewImage = image;
        }

        private void ExecuteAddIgnoreTagCommand(object obj)
        {
            if(obj == null)
                return;

            TryAddIgnoreWord(obj.ToString());
        }

        private bool CanExecuteUpdateCommand(object obj)
        {
            return Folders.Any() && !IsBusy;
        }

        private bool CanExecuteLoadFilesCommand(object obj)
        {
            return !string.IsNullOrEmpty(FolderPath) && !IsBusy;
        }

        private async void ExecuteUpdateCommand(object obj)
        {
            await Start();
        }

        private void ExecuteLoadFilesCommand(object obj)
        {
            Load();
        }

        private void ExecuteBrowseFolderCommand(object obj)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            var result = folderBrowserDialog.ShowDialog();

            if (result == DialogResult.OK)
            {
                FolderPath = folderBrowserDialog.SelectedPath;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool TryAddIgnoreWord(string word)
        {
            if (!IgnoreList.Contains(word))
            {
                IgnoreList.Add(word);

                return true;
            }

            return false;
        }

        public void Load()
        {
            Folders.Clear();
            TotalImages = 0;
            Reset();

            if (string.IsNullOrEmpty(FolderPath))
                return;

            string[] subfolders = Directory.GetDirectories(FolderPath);
            foreach (string subfolder in subfolders)
            {
                FolderModel folderModel = new FolderModel(subfolder);
                LoadImages(subfolder, folderModel);
                Folders.Add(folderModel);
            }           
        }

        private void LoadImages(string rootPath, FolderModel folderModel)
        {
            try
            {
                string[] files = Directory.GetFiles(rootPath, string.Format("*.{0}", new String[] { "jpg", "jpeg", "png", "gif", "tiff", "bmp" }), SearchOption.TopDirectoryOnly);

                foreach (string file in files)
                {
                    ImageModel imageModel = new ImageModel(FolderPath, file);
                    folderModel.Files.Add(imageModel);
                    TotalImages++;
                }
                string[] subfolders = Directory.GetDirectories(rootPath);
                foreach (string subfolder in subfolders)
                {
                    FolderModel folder = new FolderModel(subfolder);
                    folderModel.Folders.Add(folder);
                    LoadImages(subfolder, folder); // recursively parse subfolders
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        public async Task Start()
        {
            IsBusy = true;

            combinedIgnoreList = new List<string>();
            combinedIgnoreList.AddRange(predefinedIgnoreList);
            combinedIgnoreList.AddRange(IgnoreList);
            Reset();

            foreach (var folder in Folders)
            {
                await TraverseFodler(folder);
            }

            Logs.Add($"Tags updated in all images. Total images {TotalImages}. Succeded - {succesCount}, Failed - {failCount}");

            IsBusy = false;
        }

        private void Reset()
        {
            Logs.Clear();
            CurrentProgress = 0;
        }

        private async Task TraverseFodler(FolderModel model)
        {
            foreach (var mod in model.Folders)
            {
               await TraverseFodler(mod);
            }

            foreach (var item in model.Files)
            {
                Status = $"Updating " + item.Path;
                var result = await item.UpdateTags(combinedIgnoreList);
                if (!result)
                {

                    if(!string.IsNullOrEmpty(item.ErrorMessage))
                    {
                        Logs.Add($"Unable to update {item.Path}. {item.ErrorMessage}");
                    }
                    else
                    {
                        Logs.Add($"Unable to update {item.Path}.");
                    }

                    failCount++;
                }
                else
                {
                    succesCount++;
                }

                CurrentProgress++;
            }
        }

        private void Notify(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ReadUserDefinedIgnoreList()
        {
            if (File.Exists("IgnoreList.txt"))
            {
                var content = File.ReadAllText("IgnoreList.txt");

                var words = content.Split(',');
                predefinedIgnoreList.AddRange(words);
            }
        }
    } 

    //public enum ModelType
    //{
    //    Folder,
    //    Image
    //}

    //public enum ImageType
    //{
    //    Jpg,
    //    Png,
    //    Gif,
    //    Tiff
    //}
}

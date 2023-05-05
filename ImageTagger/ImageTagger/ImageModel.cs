using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CompactExifLib;

namespace ImageTagger
{
    public class ImageModel
    {
        private string rootPath;
        private string extension;
        public string Path { get; set; }
        public string Name { get; set; }
        public string? ErrorMessage { get; private set; }

        public RelayCommand PreviewCommand { get; set; }

        public ImageModel(string rootPath, string path)
        {
            this.rootPath = rootPath;
            Path = path;
            Name = System.IO.Path.GetFileName(Path);
            extension = System.IO.Path.GetExtension(Path);
            PreviewCommand = new RelayCommand(ExecutePreviewCommand);
        }

        private void ExecutePreviewCommand(object obj)
        {
            Broadcaster.RaiseEvent(Broadcaster.PreviewEvent, this);
        }

        public async Task<bool> UpdateTags(IList<string> ignoreWords)
        {

            return await Task.Run(() => DoUpdate(ignoreWords));
        }

        private bool DoUpdate(IList<string> ignoreWords)
        {
            try
            {
                BitmapEncoder encoder;

                switch (extension.ToLower())
                {
                    case ".jpg":
                    case ".jpeg":
                        var jpegencoder = new JpegBitmapEncoder();
                        jpegencoder.QualityLevel = 95;
                        encoder = jpegencoder;
                        break;
                    case ".gif":
                        encoder = new GifBitmapEncoder();
                        break;
                    case ".png":
                        encoder = new PngBitmapEncoder();
                        break;
                    case ".tiff":
                        encoder = new TiffBitmapEncoder();
                        break;
                    case ".bmp":
                        encoder = new BmpBitmapEncoder();
                        break;
                    default:
                        return false;
                }

                var creationTime = File.GetCreationTime(Path);
                var modifiedTime = File.GetLastWriteTime(Path);

                ExifData ed = new ExifData(Path);
                ExifTag tagtoUpdate = ExifTag.XpKeywords;

                if (!ed.GetTagValue(tagtoUpdate, out string xptags, StrCoding.Utf16Le_Byte))
                {
                    xptags = string.Empty;
                }

                var relativePath = System.IO.Path.GetRelativePath(rootPath, Path);
                relativePath = System.IO.Path.GetDirectoryName(relativePath);

                if (string.IsNullOrEmpty(relativePath))
                {
                    ErrorMessage = "No tags to add as image is present in the root folder.";
                    return true;
                }

                var tags = new List<string>(relativePath.Split("\\"));
                var newTags = new List<string>();
                var existingTags = xptags.Split(';', StringSplitOptions.RemoveEmptyEntries);

                if (existingTags != null && existingTags.Any())
                    newTags.AddRange(existingTags);

                foreach (var tag in tags)
                {
                    var editedTag = tag;

                    if (editedTag.Length > 4)
                    {
                        var firstFour = tag.Substring(0, 4);

                        if (int.TryParse(firstFour, out _))
                        {
                            newTags.Add(firstFour);
                            editedTag = tag.Substring(4).Trim();
                        }
                    }

                    var splittedTags = editedTag.Split(new char[] { ' ', '-', '_' });

                    foreach (var splittedTag in splittedTags)
                    {
                        if (!ignoreWords.Contains(splittedTag, StringComparer.InvariantCultureIgnoreCase) && !newTags.Contains(splittedTag))
                            newTags.Add(splittedTag);
                    }
                }

                ed.SetTagValue(tagtoUpdate, string.Join(';', newTags), StrCoding.Utf16Le_Byte);
                ed.Save(Path);

                File.SetCreationTime(Path, creationTime);
                File.SetLastWriteTime(Path, modifiedTime);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return false;
            }

            return true;
        }
    }
}

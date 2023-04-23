using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

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

                MemoryStream updatedImageStream = new MemoryStream();
                var creationTime = File.GetCreationTime(Path);
                MemoryStream originalStream = new MemoryStream();

                using (Stream originalFile = System.IO.File.Open(Path, FileMode.Open, FileAccess.ReadWrite))
                {
                    originalFile.CopyTo(originalStream);
                    originalStream.Position = 0;
                    BitmapDecoder original = BitmapDecoder.Create(originalStream,
                        BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);

                    if (original.Frames[0] != null && original.Frames[0].Metadata != null)
                    {
                        //clone the metadata from the original input image so that it can be modified
                        BitmapMetadata? metadata = original.Frames[0].Metadata.Clone() as BitmapMetadata;

                        if (metadata == null)
                        {
                            ErrorMessage = "Unable to read metadata of the Image";
                            return false;
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
                        var existingKeywords = metadata.Keywords;

                        if (existingKeywords != null && existingKeywords.Any())
                            newTags.AddRange(existingKeywords);

                        foreach (var tag in tags)
                        {
                            if (!ignoreWords.Contains(tag, StringComparer.InvariantCultureIgnoreCase))
                                newTags.Add(tag);
                        }

                        metadata.Keywords = new ReadOnlyCollection<string>(newTags);

                        encoder.Frames.Add(
                           BitmapFrame.Create(original.Frames[0], original.Frames[0].Thumbnail, metadata, original.Frames[0].ColorContexts));

                        encoder.Save(updatedImageStream);
                    }
                }

                // Save the update image by overwriting existing image.
                try
                {
                    using (var updatedFile = File.Create(Path))
                    {
                        updatedImageStream.Position = 0;
                        updatedImageStream.CopyTo(updatedFile);
                        updatedFile.Flush();
                        updatedFile.Close();
                    }
                }
                catch (Exception)
                {
                    ErrorMessage = "Error occured while saving the updated file. Reverting to the original file.";
                    using (var revertImage = File.Create(Path))
                    {
                        originalStream.Position = 0;
                        originalStream.CopyTo(revertImage);
                        revertImage.Flush();
                        revertImage.Close();
                    }
                    return false;
                }
                finally
                {
                    originalStream.Close();
                    originalStream.Dispose();
                }

                File.SetCreationTime(Path, creationTime);
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ImageTagger
{
    public class FolderModel
    {
        public string Path { get; set; }
        public string Name { get; set; }

        public List<FolderModel> Folders { get; set; }
        public List<ImageModel> Files { get; set; }

        public IEnumerable Items
        {
            get
            {
                var items = new CompositeCollection();
                items.Add(new CollectionContainer { Collection = Folders });
                items.Add(new CollectionContainer { Collection = Files });
                return items;
            }
        }

        public FolderModel(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(Path);

            Folders = new List<FolderModel>();
            Files = new List<ImageModel>();
        }
    }
}

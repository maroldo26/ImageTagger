using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageTagger
{
    public class ModelBase
    {
        public string Path { get; set; }
        public string Name { get; set; }

        public ModelBase(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(Path);
        }
    }
}

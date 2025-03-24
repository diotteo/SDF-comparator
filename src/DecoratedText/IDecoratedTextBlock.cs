using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDF_comparator {
    interface IDecoratedTextBlock : IDictionary<string, string> {
        Dictionary<string, string> Decorations { get; }
        string Text { get; set; }
    }
}

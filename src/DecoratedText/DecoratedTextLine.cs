using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SdfComparator.DecoratedText {
    public class DecoratedTextLine : IEnumerable<IDecoratedTextBlock> {
        public List<IDecoratedTextBlock> TextBlocks { get; private set; }
        public DecoratedTextLine() {
            TextBlocks = new List<IDecoratedTextBlock>();
        }

        public DecoratedTextLine(IDecoratedTextBlock txt) : this() {
            Add(txt);
        }

        public DecoratedTextLine(IEnumerable<IDecoratedTextBlock> blocks) : this() {
            foreach (var block in blocks) {
                TextBlocks.Add(block);
            }
        }

        public void Add(IDecoratedTextBlock txt) {
            TextBlocks.Add(txt);
        }

        #region IEnumerable
        IEnumerator IEnumerable.GetEnumerator() {
            return (IEnumerator)TextBlocks.GetEnumerator();
        }

        IEnumerator<IDecoratedTextBlock> IEnumerable<IDecoratedTextBlock>.GetEnumerator() {
            return TextBlocks.GetEnumerator();
        }
        #endregion
    }
}

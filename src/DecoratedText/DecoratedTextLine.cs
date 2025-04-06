using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SdfComparator.DecoratedText {
    public class DecoratedTextLine : IEnumerable<IDecoratedTextBlock>,
                IEquatable<DecoratedTextLine> {
        public List<IDecoratedTextBlock> TextBlocks { get; private set; }
        public DecoratedTextLine() {
            TextBlocks = new List<IDecoratedTextBlock>();
        }

        public DecoratedTextLine(string txt) : this() {
            Add(new DecoratedTextBlock(txt));
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

        #region IEquatable
        public bool Equals(DecoratedTextLine other) {
            if (other is null) {
                return false;
            }
            if (other.Count<IDecoratedTextBlock>() != TextBlocks.Count<IDecoratedTextBlock>()) {
                return false;
            }
            foreach ((var i, var tb) in TextBlocks.Select((tb, i) => (i, tb))) {
                var o = other.TextBlocks[i];
                if (o.Text != tb.Text || o.Decorations.Equals(tb.Decorations)) {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as DecoratedTextLine);

        public override int GetHashCode() {
            return TextBlocks.GetHashCode();
        }

        public static bool operator ==(DecoratedTextLine a, DecoratedTextLine b) {
            if (a is null) {
                return b is null;
            }
            return a.Equals(b);
        }

        public static bool operator !=(DecoratedTextLine a, DecoratedTextLine b) {
            if (a is null) {
                return !(b is null);
            }
            return !a.Equals(b);
        }
        #endregion
    }
}

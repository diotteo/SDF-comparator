﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDF_comparator {
    class DecoratedTextLine : IEnumerable<IDecoratedTextBlock> {
        public List<IDecoratedTextBlock> TextBlocks { get; private set; }


        public DecoratedTextLine(IDecoratedTextBlock txt) : this() {
            Add(txt);
        }

        public DecoratedTextLine() {
            TextBlocks = new List<IDecoratedTextBlock>();
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

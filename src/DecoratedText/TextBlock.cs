using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SdfComparator.DecoratedText {
    public class TextBlock : IDecoratedTextBlock {
        public Dictionary<string, string> Decorations { get; private set; }
        public string Text { get; set; }

        public TextBlock(string text) {
            Decorations = new Dictionary<string, string>();
            Text = text;
        }

        public TextBlock() : this("") { }

        #region IDictionary
        ICollection<string> IDictionary<string, string>.Keys => Decorations.Keys;

        ICollection<string> IDictionary<string, string>.Values => Decorations.Values;

        int ICollection<KeyValuePair<string, string>>.Count => Decorations.Count;

        bool ICollection<KeyValuePair<string, string>>.IsReadOnly => true;

        string IDictionary<string, string>.this[string key] { get => throw new KeyNotFoundException(); set => throw new NotSupportedException(); }

        bool IDictionary<string, string>.ContainsKey(string key) => false;

        void IDictionary<string, string>.Add(string key, string value) { }

        bool IDictionary<string, string>.Remove(string key) => false;

        bool IDictionary<string, string>.TryGetValue(string key, out string value) {
            value = null;
            return false;
        }

        void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item) { }

        void ICollection<KeyValuePair<string, string>>.Clear() { }

        bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item) => false;

        void ICollection<KeyValuePair<string, string>>.CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) { }

        bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item) => false;

        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator() => Decorations.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Decorations.GetEnumerator();
        #endregion
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDF_comparator {
    class DecoratedTextBlock : IDecoratedTextBlock {
        public Dictionary<string, string> Decorations { get; private set; }
        public string Text { get; set; }

        public DecoratedTextBlock(string text) {
            Decorations = new Dictionary<string, string>();
            Text = text;
        }

        public DecoratedTextBlock() : this("") { }

        public DecoratedTextBlock WithDecoration(string name, string value) {
            Decorations[name] = value;
            return this;
        }

        #region IDictionary
        ICollection<string> IDictionary<string, string>.Keys => Decorations.Keys;

        ICollection<string> IDictionary<string, string>.Values => Decorations.Values;

        int ICollection<KeyValuePair<string, string>>.Count => Decorations.Count;

        bool ICollection<KeyValuePair<string, string>>.IsReadOnly => false;

        string IDictionary<string, string>.this[string key] { get => Decorations[key]; set => Decorations[key] = value; }

        bool IDictionary<string, string>.ContainsKey(string key) => Decorations.ContainsKey(key);

        void IDictionary<string, string>.Add(string key, string value) => Decorations.Add(key, value);

        bool IDictionary<string, string>.Remove(string key) => Decorations.Remove(key);

        bool IDictionary<string, string>.TryGetValue(string key, out string value) => Decorations.TryGetValue(key, out value);

        void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item) => (Decorations as ICollection<KeyValuePair<string, string>>).Add(item);

        void ICollection<KeyValuePair<string, string>>.Clear() => Decorations.Clear();

        bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item) => Decorations.Contains(item);

        void ICollection<KeyValuePair<string, string>>.CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) => (Decorations as ICollection<KeyValuePair<string, string>>).CopyTo(array, arrayIndex);

        bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item) => (Decorations as ICollection<KeyValuePair<string, string>>).Remove(item);

        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator() {
            return Decorations.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return Decorations.GetEnumerator();
        }
        #endregion
    }
}

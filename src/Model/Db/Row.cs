using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

namespace SdfComparator.Model.Db {
    public class Row : IEnumerable<object> {
        private readonly object[] raw;


        public Row(object[] raw) {
            this.raw = raw.Clone() as object[];
        }

        public int Length { get { return raw.Length; } }

        public object this[int i] { get { return raw[i]; } }

        public override string ToString() {
            string s = "";
            string sep = "";
            foreach (var col in raw) {
                s += $"{sep}{col}";
                sep = " | ";
            }
            return s;
        }

        public IEnumerator GetEnumerator() {
            return raw.GetEnumerator();
        }

        IEnumerator<object> IEnumerable<object>.GetEnumerator() {
            return ((IEnumerable<object>)raw).GetEnumerator();
        }
    }
}

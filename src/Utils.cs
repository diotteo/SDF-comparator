using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SdfComparator {
    class Utils {
        public static void WriteDiffLine(string s, bool b_is_add) {
            if (!b_is_add) {
                Console.ForegroundColor = ConsoleColor.Red;
            } else {
                Console.ForegroundColor = ConsoleColor.Green;
            }
            WriteLine(s);
            Console.ResetColor();
        }

        public static void WriteLine(string s) {
#if DEBUG
            Debug.WriteLine(s);
#else
            Console.WriteLine(s);
#endif
        }
    }

    public class ReverseIntComparer : IComparer<int> {
        int IComparer<int>.Compare(int a, int b) {
            return b - a;
        }
    }
}

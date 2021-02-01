using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SDF_comparator {
    class Utils {
        public static void WriteLine(string s) {
#if DEBUG
            Debug.WriteLine(s);
#else
            Console.WriteLine(s);
#endif
        }
    }
}

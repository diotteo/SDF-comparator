using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SqlServerCe;
using System.Diagnostics;
using NDesk.Options;

namespace SdfComparator {
    class SdfComparator {
        static int Main(string[] args) {
            var settings = new Model.Settings();
            View.IView view = new View.ConsoleView();
            var pstr = new Presenter.Presenter(settings, view);
            
            var ret = pstr.ParseArgs(args);
            if (ret != -1) {
                return ret;
            }

            pstr.Run();

            return 0;
        }
    }
}
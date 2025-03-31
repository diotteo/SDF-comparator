using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SdfComparator.Model {
    public class Settings {
        public string[] Filepaths { get; set; }
        public bool ShowHelp { get; set; }
        public bool ShowVersion { get; set; }
        public bool ShowCols { get; set; }
        public bool ShowRows { get; set; }
        public bool ShowTables { get; set; }
        public bool ShowAll { get; set; }
        public bool DoColors { get; set; }

        private string _prgm;
        public string PrgmName {
            get => _prgm;
            set {
                if (_prgm != value) {
                    _prgm = value;
                }
            }
        }

        private string _version;
        public string Version {
            get => _version;
            set {
                if (_version != value) {
                    _version = value;
                }
            }
        }

        public string HelpText { get; set; }

        public Settings() {
        }
    }
}

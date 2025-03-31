using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NDesk.Options;

using SdfComparator.Model.Db;
using SdfComparator.DecoratedText;

namespace SdfComparator.Presenter {
    class Presenter {
        private Model.Settings settings;
        private View.IView view;

        public Presenter(Model.Settings settings, View.IView view) {
            this.settings = settings;
            this.view = view;

            var prgm_name = System.IO.Path.GetFileName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
            var version = typeof(SdfComparator).Assembly.GetName().Version.ToString();
            this.settings.PrgmName = prgm_name;
            this.settings.Version = version;
        }

        public string GetPrgmName() {
            return settings.PrgmName;
        }

        public string GetHelpText() {
            return settings.HelpText;
        }

        public bool DoColors() {
            return settings.DoColors;
        }

        public int ParseArgs(string[] args) {
            settings.ShowHelp = false;
            settings.ShowVersion = false;
            settings.ShowCols = false;
            settings.ShowRows = false;
            settings.ShowTables = false;
            settings.DoColors = true;

            var p = new OptionSet() {
                {"h|help", "Show this help message and exit", v => settings.ShowHelp = v != null},
                {"v|version", "Print the version and exit", v => settings.ShowVersion = v != null},
                {"c|col", "Print column differences", v => settings.ShowCols = v != null},
                {"r|row", "Print each row differences", v => settings.ShowRows = v != null},
                {"t|table", "Print table differences", v => settings.ShowTables = v != null},
                {"no-color", "Disable colored output", v => settings.DoColors = v != null},
            };
            var filepaths = p.Parse(args);

            var buffer = new System.IO.StringWriter();
            p.WriteOptionDescriptions(buffer);
            settings.HelpText = buffer.ToString();

            if (settings.ShowHelp) {
                view.ShowHelp(this);
                return 0;
            } else if (settings.ShowVersion) {
                view.ShowMessage(this, $"{settings.PrgmName} v{settings.Version}");
                return 0;
            } else if (filepaths.Count != 2) {
                view.ShowMessage(this, "Error: 2 files required");
                view.ShowHelp(this);
                return 1;
            }

            settings.Filepaths = filepaths.ToArray();
            foreach (var fpath in settings.Filepaths) {
                if (!System.IO.File.Exists(fpath)) {
                    view.ShowMessage(this, $"Error: \"{fpath}\" is not a file");
                    view.ShowHelp(this);
                    return 1;
                }
            }
            if (!settings.ShowTables && !settings.ShowCols && !settings.ShowRows) {
                settings.ShowAll = true;
            } else {
                settings.ShowAll = false;
            }

            return -1;
        }

        public bool DoShowTables() {
            return settings.ShowAll || settings.ShowTables;
        }

        public bool DoShowColumns() {
            return settings.ShowAll || settings.ShowCols;
        }

        public bool DoShowRows() {
            return settings.ShowAll || settings.ShowRows;
        }

        //FIXME: Most of this should be View-specific
        public void Run() {
            var db_tup = new DatabaseTuple(settings.Filepaths[0], settings.Filepaths[1]);

            view.ShowDiffs(this, db_tup);
        }
    }
}

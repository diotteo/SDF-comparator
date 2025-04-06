using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using SdfComparator.DecoratedText;
using SdfComparator.Model.Db;

namespace SdfComparator.View {
    public class ConsoleView: IView {
        public ConsoleView() {
        }

        public void ShowHelp(Presenter.Presenter pstr) {
            Console.WriteLine($"Usage: {pstr.GetPrgmName()} [options] {{path/to/file1.sdf}} {{path/to/file2.sdf}}");
            Console.WriteLine(pstr.GetHelpText());
        }

        public void ShowMessage(Presenter.Presenter pstr, string msg) {
            Console.WriteLine(msg);
        }

        public void ShowDecoratedLine(Presenter.Presenter pstr, DecoratedTextLine line) {
            foreach (var block in line) {
                if (pstr.DoColors() && block.TryGetValue("color", out var color)) {
                    switch (color) {
                        case "green":
                            Console.ForegroundColor = ConsoleColor.Green;
                            break;
                        case "red":
                            Console.ForegroundColor = ConsoleColor.Red;
                            break;
                    }
                }
                Console.Write(block.Text);
                if (pstr.DoColors()) {
                    Console.ResetColor();
                }
            }
            Console.Write("\n");
        }

        public void ShowDecoratedLines(
                Presenter.Presenter pstr,
                List<DecoratedTextLine> lines) {
            foreach (var line in lines) {
                ShowDecoratedLine(pstr, line);
            }
        }

        public bool ShowDecoratedSection(
                Presenter.Presenter pstr,
                List<DecoratedTextLine> headers,
                List<DecoratedTextLine> lines) {
            bool b_print = (lines.Count > 0);

            if (b_print) {
                foreach (var header in headers) {
                    ShowDecoratedLine(pstr, header);
                }
            }
            ShowDecoratedLines(pstr, lines);

            return b_print;
        }

        public void ShowDiffs(Presenter.Presenter pstr, DatabaseTuple db_tup) {
            var headers = pstr.InitHeaders(db_tup);

            List<DecoratedTextLine> lines;
            if (pstr.DoShowTables()) {
                lines = pstr.GetTableDiffLines(db_tup);
                if (ShowDecoratedSection(pstr, headers, lines)) {
                    headers.Clear();
                }
            }

            lines = pstr.GetTableRowDiffLines(db_tup, headers);
            headers.Clear();
            ShowDecoratedSection(pstr, headers, lines);
        }
    }
}

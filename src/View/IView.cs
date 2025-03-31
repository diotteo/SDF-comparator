using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SdfComparator.DecoratedText;
using SdfComparator.Model.Db;

namespace SdfComparator.View {
    interface IView {
        void ShowHelp(Presenter.Presenter pstr);
        void ShowMessage(Presenter.Presenter pstr, string msg);
        void ShowDiffs(Presenter.Presenter pstr, DatabaseTuple db_tup);
    }
}

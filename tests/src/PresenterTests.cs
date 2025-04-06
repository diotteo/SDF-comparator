using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

using SdfComparator.DecoratedText;
using SdfComparator.Model;

namespace SdfComparator.Tests {
    [TestClass]
    public class PresenterTests {
        [TestMethod]
        public void TestNew() {
            var s = new Settings();
            var p = new Presenter.Presenter(s, null);

            Assert.AreEqual("SdfComparator.exe", p.GetPrgmName());
            Assert.IsTrue(p.GetVersion().Length > 2);
        }

        [TestMethod]
        public void TestDefaultColor() {
            var s = new Settings();
            var p = new Presenter.Presenter(s, null);

            Assert.AreEqual(p.ParseArgs(new string[] {
                        "..\\..\\..\\test tables\\same1.sdf",
                        "..\\..\\..\\test tables\\same2.sdf"
                    }), -1);
            Assert.IsTrue(p.DoColors());
        }

        [TestMethod]
        public void TestInitHeaders() {
            //Arrange
            var s = new Settings();
            var p = new Presenter.Presenter(s, null);
            var files = new string[] {
                    "C:\\fake\\path\\db1.sdf",
                    "C:\\fake\\path\\db2.sdf"
                    };
            var db_tup = new Model.Db.DatabaseTuple(
                    files[0],
                    files[1]);
            var expected = new List<DecoratedTextLine> {
                    new DecoratedTextLine($"--- {files[0]}"),
                    new DecoratedTextLine($"+++ {files[1]}"),
                    new DecoratedTextLine("")
                    };

            //Act
            var actual = p.InitHeaders(db_tup);

            //Assert
            Assert.IsTrue(expected.SequenceEqual<DecoratedTextLine>(actual));
        }

        [TestMethod]
        public void TestGetPaddedString() {
            //Arrange
            var r = new Model.Db.Row(new object[] {
                    "foo",
                    20,
                    "flurbh"
                    });
            var mlm = new List<int> { 5, 3, 4 };
            var c = '@';

            var expected = " @ | foo   | 20  | flurbh |";

            //Act
            var actual = Presenter.Presenter.GetPaddedString(r, mlm, c);

            //Assert
            Assert.AreEqual(expected, actual);
        }
    }
}

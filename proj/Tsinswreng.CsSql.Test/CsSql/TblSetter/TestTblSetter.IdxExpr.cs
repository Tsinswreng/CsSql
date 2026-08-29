using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;
using Tsinswreng.CsTreeTest;

namespace Tsinswreng.CsSql.Test.CsSql.TblSetter;

public partial class TestTblSetter {

	void RegisterIdxExpr(ITestNode Node) {
		var register = Node.MkTestFnRegister(
			typeof(TestTblSetter)
			,[typeof(ITblSetter<TestKv>)]
			,[nameof(ITblSetter<TestKv>.FnSetIdx), nameof(ITblSetter<TestKv>.IdxExpr)]
			,nameof(TestTblSetter)
		);
		var R = register.Register;
		var T = Assert.IsTrue;

		R("IdxExpr_SingleMember_AppendsExactFnSetIdxSql", async(o)=>{
			var s = MkTblSetter();
			AssertFnSetIdxPointsToDefault(s, "IdxExpr_SingleMember_AppendsExactFnSetIdxSql");
			var t = s.Tbl;
			t.OuterAdditionalSqls.Clear();

			s.IdxExpr(null, x => x.KStr);
			var expected = new List<str>{
$"""
CREATE INDEX "Idx_TestKv_KStr"
ON "TestKv" ("KStr")
"""
			};

			AssertSqlListExact(t.OuterAdditionalSqls, expected, "IdxExpr_SingleMember_AppendsExactFnSetIdxSql");
			return NIL;
		});

		R("IdxExpr_CompositeExpression_AppendsExactFnSetIdxSql", async(o)=>{
			var s = MkTblSetter();
			AssertFnSetIdxPointsToDefault(s, "IdxExpr_CompositeExpression_AppendsExactFnSetIdxSql");
			var t = s.Tbl;
			t.OuterAdditionalSqls.Clear();

			s.IdxExpr(null, x => new {x.Owner, x.VStr});
			var expected = new List<str>{
$"""
CREATE INDEX "Idx_TestKv_Owner_VStr"
ON "TestKv" ("Owner", "VStr")
"""
			};

			AssertSqlListExact(t.OuterAdditionalSqls, expected, "IdxExpr_CompositeExpression_AppendsExactFnSetIdxSql");
			return NIL;
		});

		R("IdxExpr_UniqueWhere_MultiExpressions_ExactSqlList", async(o)=>{
			var s = MkTblSetter();
			AssertFnSetIdxPointsToDefault(s, "IdxExpr_UniqueWhere_MultiExpressions_ExactSqlList");
			var t = s.Tbl;
			t.OuterAdditionalSqls.Clear();

			s.IdxExpr(
				new OptMkIdx{
					Unique = true,
					Where = t.SqlIsNonDel()
				},
				x => x.KStr,
				x => new {x.Owner, x.VStr}
			);
			var expected = new List<str>{
$"""
CREATE UNIQUE INDEX "Ux_TestKv_KStr"
ON "TestKv" ("KStr")
WHERE ("DelAt" = 0)
""",
$"""
CREATE UNIQUE INDEX "Ux_TestKv_Owner_VStr"
ON "TestKv" ("Owner", "VStr")
WHERE ("DelAt" = 0)
"""
			};

			AssertSqlListExact(t.OuterAdditionalSqls, expected, "IdxExpr_UniqueWhere_MultiExpressions_ExactSqlList");
			return NIL;
		});

		R("IdxExpr_CustomFnSetIdx_ReceivesParsedColsInOrder", async(o)=>{
			var s = MkTblSetter();
			var t = s.Tbl;
			t.OuterAdditionalSqls.Clear();
			List<List<str>> captured = [];
			var expected = new List<str>{"R1", "R2"};

			s.FnSetIdx = (opt, tbl, cols) => {
				foreach (var colSet in cols) {
					captured.Add(colSet.ToList());
				}
				return expected;
			};

			s.IdxExpr(
				null,
				x => x.KStr,
				x => new {x.Owner, x.VStr}
			);

			AssertSqlListExact(t.OuterAdditionalSqls, expected, "IdxExpr_CustomFnSetIdx_ReceivesParsedColsInOrder");
			T(captured.Count == 2, $"Expected captured 2 col sets, got {captured.Count}");
			T(captured[0].Count == 1 && captured[0][0] == nameof(TestKv.KStr), "First expression columns mismatch");
			T(captured[1].Count == 2 && captured[1][0] == nameof(TestKv.Owner) && captured[1][1] == nameof(TestKv.VStr),
				"Second expression columns mismatch");
			return NIL;
		});

		R("IdxExpr_EmptyExpressions_AppendsNoSql", async(o)=>{
			var s = MkTblSetter();
			AssertFnSetIdxPointsToDefault(s, "IdxExpr_EmptyExpressions_AppendsNoSql");
			var t = s.Tbl;
			t.OuterAdditionalSqls.Clear();

			s.IdxExpr(null);
			AssertSqlListExact(t.OuterAdditionalSqls, [], "IdxExpr_EmptyExpressions_AppendsNoSql");
			return NIL;
		});
	}
}
